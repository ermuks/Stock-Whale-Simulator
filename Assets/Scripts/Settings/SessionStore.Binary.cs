using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 체크포인트의 비동기 저장과 바이너리 파일 형식.
/// 1) 세계 상태 수집은 메인 스레드에서 프레임마다 조금씩 한다(세계가 멈춘 마감 보고 상태에서만).
/// 2) 수집한 자료의 압축·암호화·서명·디스크 기록·검증은 백그라운드 스레드에서 흘려 쓴다. 큰 문자열을 만들지 않는다.
/// 파일: 머리말(식별자 8, 버전 4, IV 16) + AES(Deflate(JSON)) + HMAC-SHA256(32).
/// 키는 게임에 들어 있으므로 메모장으로 고치는 수준의 변조를 막는 용도다. 작정한 분석까지 막지는 못한다.
/// </summary>
public static partial class SessionStore
{
    private const string BinaryMarker = "@dondon-binary:";
    private const int BinaryVersion = 1, HeaderLength = 28, MacLength = 32;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("DDSAVE3\0");
    private static SaveJob pending;

    private static byte[] DeriveKey(string purpose)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes("DonDon.session." + purpose + ".9f41c7d2a6e80b35.TradeScene"));
    }

    public sealed class SaveJob
    {
        public enum Stage
        {
            Capturing,
            Writing,
            Done,
            Failed
        }

        private Graph writer = new();
        private MonoBehaviour[] roots;
        private JArray bindings = new();
        private JObject data;
        private Task task;
        private int index;
        private volatile Stage state = Stage.Capturing;
        private volatile float progress;
        public Stage State => state;
        /// <summary>0~1. 수집이 60%, 기록·검증이 40%.</summary>
        public float Progress => progress;
        public Exception Error { get; private set; }
        public long FileBytes { get; private set; }
        public int NodeCount { get; private set; }
        public bool IsCompleted => state == Stage.Done || state == Stage.Failed;
        public bool CaptureFinished => state != Stage.Capturing;

        internal SaveJob()
        {
            roots = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Where(Root).OrderBy(Key, StringComparer.Ordinal).ToArray();
        }

        /// <summary>프레임당 정해진 시간만 쓰며 세계 상태를 수집한다. 수집이 끝나면 기록 단계가 백그라운드에서 시작된다.</summary>
        public IEnumerator Capture(float budgetSeconds = 0.012f)
        {
            while (state == Stage.Capturing && index < roots.Length)
            {
                float start = Time.realtimeSinceStartup;
                while (state == Stage.Capturing && index < roots.Length && Time.realtimeSinceStartup - start < budgetSeconds)
                {
                    Step();
                }

                yield return null;
            }

            FinishCapture();
        }

        /// <summary>남은 수집을 지금 끝낸다. 세계가 다시 움직이기 직전에 호출해 저장 내용의 일관성을 지킨다.</summary>
        public void CompleteCaptureNow()
        {
            while (state == Stage.Capturing && index < roots.Length)
            {
                Step();
            }

            FinishCapture();
        }

        private void Step()
        {
            try
            {
                var component = roots[index++];
                if (component != null)
                {
                    bindings.Add(new JObject { ["key"] = Key(component), ["value"] = writer.WriteComponent(component), ["enabled"] = component.enabled, ["active"] = component.gameObject.activeSelf });
                }

                progress = 0.6f * index / Mathf.Max(1, roots.Length);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void FinishCapture()
        {
            if (state != Stage.Capturing)
            { return; }

            try
            {
                data = new JObject { ["schema"] = Schema, ["scene"] = "TradeScene", ["clock"] = SessionClock.Time,
                    ["random"] = JsonUtility.ToJson(UnityEngine.Random.state), ["roots"] = bindings, ["nodes"] = writer.Nodes, ["types"] = writer.Types,
                    ["newsCooldown"] = FakeNewsManager.Instance?.PlayerCooldown ?? 0, ["savedUtc"] = DateTime.UtcNow.Ticks,
                    ["socialId"] = typeof(ChatSocialIds).GetField("last", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null).ToString() };
                NodeCount = writer.Nodes.Count;
                string path = PathName;
                writer = null;
                roots = null;
                bindings = null;
                state = Stage.Writing;
                task = Task.Run(() => WriteFile(path));
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void Fail(Exception exception)
        {
            Error = exception;
            writer = null;
            roots = null;
            bindings = null;
            data = null;
            state = Stage.Failed;
        }

        // 백그라운드 스레드. Unity API를 쓰지 않는다. data는 수집이 끝난 뒤 누구도 건드리지 않는 순수 자료다.
        private void WriteFile(string path)
        {
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                WriteBinary(temp, data, fraction => progress = 0.6f + 0.3f * fraction);
                data = null;
                long length = new FileInfo(temp).Length;
                if (length > MaxBytes)
                { throw new InvalidOperationException("Checkpoint exceeds storage limit."); }

                // 교체 전에 디스크에 쓴 내용을 다시 읽어 서명을 확인한다. 실패하면 이전 저장이 그대로 남는다.
                VerifyBinary(temp);
                progress = 0.97f;
                if (File.Exists(path))
                {
                    File.Replace(temp, path, path + ".bak");
                }
                else
                {
                    File.Move(temp, path);
                }

                FileBytes = length;
                progress = 1f;
                state = Stage.Done;
            }
            catch (Exception exception)
            {
                data = null;
                Error = exception;
                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch (IOException)
                {
                }

                state = Stage.Failed;
            }
        }

        /// <summary>기록 단계가 끝날 때까지 기다린다. 수집이 끝난 뒤에만 의미가 있다.</summary>
        public void Wait()
        {
            task?.Wait();
        }
    }

    /// <summary>마감 체크포인트 저장을 시작한다. 반환된 작업의 Capture를 코루틴으로 돌린다.</summary>
    public static SaveJob BeginSave()
    {
        if (!CanSave)
        { throw new InvalidOperationException("Only a completed closing report can create a checkpoint."); }

        WaitForPendingSave();
        pending = new SaveJob();
        return pending;
    }

    /// <summary>진행 중인 저장이 있으면 끝날 때까지 기다린다. 불러오기·종료 전에 호출한다.</summary>
    public static void WaitForPendingSave()
    {
        var job = pending;
        if (job == null)
        { return; }

        if (!job.CaptureFinished)
        {
            job.CompleteCaptureNow();
        }

        job.Wait();
        pending = null;
    }

    public static bool SaveInProgress => pending != null && !pending.IsCompleted;

    private static void WriteBinary(string file, JObject data, Action<float> report)
    {
        byte[] iv = new byte[16];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(iv);
        }

        using var output = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        using var mac = new HMACSHA256(DeriveKey("mac"));
        var signed = new SigningStream(output, mac);
        signed.Write(Magic, 0, Magic.Length);
        signed.Write(BitConverter.GetBytes(BinaryVersion), 0, 4);
        signed.Write(iv, 0, iv.Length);
        using (var aes = Aes.Create())
        {
            aes.Key = DeriveKey("data");
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            using var crypto = new CryptoStream(signed, encryptor, CryptoStreamMode.Write);
            using (var deflate = new DeflateStream(crypto, System.IO.Compression.CompressionLevel.Fastest, true))
            using (var text = new StreamWriter(deflate, new UTF8Encoding(false), 1 << 16, true))
            using (var json = new JsonTextWriter(text) { CloseOutput = false, Formatting = Formatting.None })
            {
                // 최상위 항목을 하나씩 흘려 쓴다. 가장 큰 nodes는 원소 단위로 써서 진행률을 알린다.
                json.WriteStartObject();
                foreach (var property in data.Properties())
                {
                    json.WritePropertyName(property.Name);
                    if (property.Name == "nodes" && property.Value is JArray nodes)
                    {
                        json.WriteStartArray();
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            nodes[i].WriteTo(json);
                            if ((i & 4095) == 0)
                            {
                                report(i / (float)Math.Max(1, nodes.Count));
                            }
                        }

                        json.WriteEndArray();
                    }
                    else
                    {
                        property.Value.WriteTo(json);
                    }
                }

                json.WriteEndObject();
            }

            crypto.FlushFinalBlock();
        }

        mac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        output.Write(mac.Hash, 0, MacLength);
        output.Flush(true);
        report(1f);
    }

    /// <summary>서명만 확인한다. 내용을 풀지 않으므로 메모리를 거의 쓰지 않는다.</summary>
    private static void VerifyBinary(string file)
    {
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        long length = input.Length;
        if (length < HeaderLength + MacLength || length > MaxBytes * 2L)
        { throw new InvalidDataException("Checkpoint is damaged or incompatible."); }

        byte[] buffer = new byte[1 << 16];
        using var mac = new HMACSHA256(DeriveKey("mac"));
        long remaining = length - MacLength;
        bool header = true;
        while (remaining > 0)
        {
            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read <= 0)
            { throw new InvalidDataException("Checkpoint is damaged or incompatible."); }

            if (header)
            {
                header = false;
                if (read < HeaderLength || !Magic.SequenceEqual(buffer.Take(Magic.Length)) || BitConverter.ToInt32(buffer, Magic.Length) != BinaryVersion)
                { throw new InvalidDataException("Checkpoint is damaged or incompatible."); }
            }

            mac.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        mac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        byte[] stored = new byte[MacLength];
        int got = 0;
        while (got < MacLength)
        {
            int read = input.Read(stored, got, MacLength - got);
            if (read <= 0)
            { break; }

            got += read;
        }

        int difference = got == MacLength ? 0 : 1;
        byte[] expected = mac.Hash;
        for (int i = 0; i < MacLength; i++)
        {
            difference |= stored[i] ^ expected[i];
        }

        if (difference != 0)
        { throw new InvalidDataException("Checkpoint is damaged or incompatible."); }
    }

    private static JObject LoadBinary(string file)
    {
        VerifyBinary(file);
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        byte[] header = new byte[HeaderLength];
        if (input.Read(header, 0, HeaderLength) != HeaderLength)
        { throw new InvalidDataException("Checkpoint is damaged or incompatible."); }

        byte[] iv = new byte[16];
        Array.Copy(header, 12, iv, 0, 16);
        using var aes = Aes.Create();
        aes.Key = DeriveKey("data");
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        using var body = new BoundedStream(input, input.Length - HeaderLength - MacLength);
        using var crypto = new CryptoStream(body, decryptor, CryptoStreamMode.Read);
        using var inflate = new DeflateStream(crypto, CompressionMode.Decompress);
        using var text = new StreamReader(inflate, new UTF8Encoding(false), false, 1 << 16);
        using var reader = new JsonTextReader(text) { MaxDepth = 128, DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
    }

    /// <summary>쓰는 바이트를 서명에 넣으면서 파일로 넘긴다. 닫아도 파일은 닫지 않는다.</summary>
    private sealed class SigningStream : Stream
    {
        private readonly Stream inner;
        private readonly HMACSHA256 mac;
        public SigningStream(Stream inner, HMACSHA256 mac)
        {
            this.inner = inner;
            this.mac = mac;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { inner.Flush(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            mac.TransformBlock(buffer, offset, count, null, 0);
            inner.Write(buffer, offset, count);
        }
    }

    /// <summary>파일 끝의 서명을 본문으로 읽지 않도록 길이를 제한한다.</summary>
    private sealed class BoundedStream : Stream
    {
        private readonly Stream inner;
        private long remaining;
        public BoundedStream(Stream inner, long length)
        {
            this.inner = inner;
            remaining = Math.Max(0, length);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (remaining <= 0)
            { return 0; }

            int read = inner.Read(buffer, offset, (int)Math.Min(count, remaining));
            remaining -= read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
