const itCompanyData = {
  "companyType": "상장종목",
  "startNodeId": 1000,
  "nodes": [
    {
      "id": 1000,
      "description": "신규 상장 - 소량 거래만 이루어지는 초기",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1001, 1002, 1003],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 0.016,
      "stayTimeRangeMax": 0.5
    },
    {
      "id": 1001,
      "description": "첫 대형 수주 계약 체결 - 실적 기대감 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1004, 1005, 1006],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1002,
      "description": "유동성 우려 - 추가 자금 조달 실패 소식",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.85,
      "next": [1007, 1008, 1009],
      "nextWeight": [35, 40, 25],
      "stayTimeRangeMin": 6.1,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1003,
      "description": "핵심 인력 영입 - 기술력 강화 기대",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1010, 1011],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.9,
      "stayTimeRangeMax": 11.7
    },
    {
      "id": 1004,
      "description": "대형 수주 납기 완료 - 주요 거래처 만족도 호평",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1012, 1013, 1014],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.2
    },
    {
      "id": 1005,
      "description": "수주 사업 일정 지연 - 실적 부담론 대두",
      "criticalValueMin": 0.5,
      "criticalValueMax": 0.95,
      "next": [1015, 1016, 1017],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 7.3,
      "stayTimeRangeMax": 16.5
    },
    {
      "id": 1006,
      "description": "품질 이슈 보도 - 일부 거래처 불만 제기",
      "criticalValueMin": 0.57,
      "criticalValueMax": 0.75,
      "next": [1018, 1019, 1020],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 8.1,
      "stayTimeRangeMax": 17.9
    },
    {
      "id": 1007,
      "description": "엔젤 투자 유치 성공 - 유동성 개선 기대",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1021, 1022, 1023],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 5.8,
      "stayTimeRangeMax": 14.2
    },
    {
      "id": 1008,
      "description": "비용 연체 의혹 - 사업장 유지 부담론",
      "criticalValueMin": 0.55,
      "criticalValueMax": 0.6,
      "next": [1024, 1025, 1801, 1802],
      "nextWeight": [70, 20, 6, 4],
      "stayTimeRangeMin": 7.5,
      "stayTimeRangeMax": 16.8
    },
    {
      "id": 1009,
      "description": "구조 개편 - 비용 절감 목적 사업장 이전",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1026, 1027, 1028],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 6.4,
      "stayTimeRangeMax": 15.6
    },
    {
      "id": 1010,
      "description": "신규 사업 투자 착수 - 자체 제품 라인 개발",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1029, 1030],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.7,
      "stayTimeRangeMax": 13.9
    },
    {
      "id": 1011,
      "description": "대기업 수주 계약 체결 - 대형 수주 수주",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1031, 1032],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.4
    },
    {
      "id": 1012,
      "description": "추가 수주 계약 연이어 체결 - 실적 호조 전망",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1301, 1302, 1303],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.6,
      "stayTimeRangeMax": 11.8
    },
    {
      "id": 1013,
      "description": "사업 포트폴리오 강화 - 브랜드 가치 상승 기대",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1304, 1305, 1306],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 5.1,
      "stayTimeRangeMax": 13.7
    },
    {
      "id": 1014,
      "description": "인력 부족 우려 - 수주 증가로 인력 수급 부담",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.05,
      "next": [1307, 1308, 1309],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 6.9,
      "stayTimeRangeMax": 15.3
    },
    {
      "id": 1015,
      "description": "주요 거래처와 재협상 - 납기·조건 조정 합의",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1310, 1311],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.7,
      "stayTimeRangeMax": 15.9
    },
    {
      "id": 1016,
      "description": "거래처 신뢰 회복 - 추가 수주 수주 진행",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1312, 1313],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.6,
      "stayTimeRangeMax": 13.4
    },
    {
      "id": 1017,
      "description": "수익성 악화 우려 - 추가 비용으로 영업이익 부담",
      "criticalValueMin": 0.55,
      "criticalValueMax": 1,
      "next": [1314, 1315],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.2
    },
    {
      "id": 1018,
      "description": "품질 개선 작업 가속 - 사후 관리 강화",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.1,
      "next": [1316, 1317, 1318],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 8.3,
      "stayTimeRangeMax": 17.1
    },
    {
      "id": 1019,
      "description": "주요 거래처 이탈 - 계약 종료로 실적 부담",
      "criticalValueMin": 0.55,
      "criticalValueMax": 0.7,
      "next": [1319, 1320, 1803, 1804],
      "nextWeight": [65, 25, 6, 4],
      "stayTimeRangeMin": 9.5,
      "stayTimeRangeMax": 18.7
    },
    {
      "id": 1020,
      "description": "부정적 보도 확산 - 평판 관리 필요성 대두",
      "criticalValueMin": 0.63,
      "criticalValueMax": 0.85,
      "next": [1321, 1322],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.7,
      "stayTimeRangeMax": 17.5
    },
    {
      "id": 1021,
      "description": "투자금 투입 사업장 확장 - 성장성 부각",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1323, 1324, 1325],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 5.9,
      "stayTimeRangeMax": 14.3
    },
    {
      "id": 1022,
      "description": "마케팅 예산 확보 - 온라인 홍보 시작",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1326, 1327, 1328],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 15.0
    },
    {
      "id": 1023,
      "description": "개발 도구 구매 - 생산성 향상을 위한 도구 도입",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1329, 1330],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.9
    },
    {
      "id": 1024,
      "description": "운영비 협상 성공 - 집주인과 할인 합의",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1331, 1332],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.4,
      "stayTimeRangeMax": 16.6
    },
    {
      "id": 1025,
      "description": "퇴거 위기 - 사업장에서 나가야 할 상황",
      "criticalValueMin": 0.53,
      "criticalValueMax": 0.55,
      "next": [1333, 1805, 1806],
      "nextWeight": [92, 5, 3],
      "stayTimeRangeMin": 8.6,
      "stayTimeRangeMax": 17.8
    },
    {
      "id": 1026,
      "description": "원격 협업 시스템 구축 - 효율적인 원격 근무 환경 조성",
      "criticalValueMin": 1,
      "criticalValueMax": 1.09,
      "next": [1334, 1335, 1336],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 6.3,
      "stayTimeRangeMax": 15.5
    },
    {
      "id": 1027,
      "description": "소통 문제 발생 - 원격 근무로 인한 업무 효율 저하",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1337, 1338],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.6,
      "stayTimeRangeMax": 16.8
    },
    {
      "id": 1028,
      "description": "비용 절감 효과 - 운영비 대폭 감소",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.09,
      "next": [1339, 1340],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.8,
      "stayTimeRangeMax": 14.2
    },
    {
      "id": 1029,
      "description": "MVP 출시 - 최소 기능 제품으로 시장 진입",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1341, 1342, 1343],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 5.7,
      "stayTimeRangeMax": 13.9
    },
    {
      "id": 1030,
      "description": "개발 지연 - 기술적 난제로 일정 지연",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.05,
      "next": [1344, 1345],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.2,
      "stayTimeRangeMax": 17.4
    },
    {
      "id": 1031,
      "description": "대규모 수주 사업 성공 - 고객 만족도 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1346, 1347, 1348],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.7,
      "stayTimeRangeMax": 12.3
    },
    {
      "id": 1032,
      "description": "추천 수주 증가 - 대기업 네트워크 확장",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1349, 1350],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 13.6
    },
    {
      "id": 1301,
      "description": "동시 진행 수주 사업 증가 - 수익성 크게 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1351, 1352, 1353],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.9,
      "stayTimeRangeMax": 12.5
    },
    {
      "id": 1302,
      "description": "브랜드 인지도 상승 - 업계에서 주목받기 시작",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1354, 1355, 1356],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 5.3,
      "stayTimeRangeMax": 13.7
    },
    {
      "id": 1303,
      "description": "수주 사업 관리 어려움 - 동시 진행으로 품질 관리 힘듦",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.05,
      "next": [1357, 1358, 1359],
      "nextWeight": [40, 35, 25],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1304,
      "description": "포트폴리오 사이트 개편 - 전문성 어필 강화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1360, 1361, 1362],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 5.7,
      "stayTimeRangeMax": 14.3
    },
    {
      "id": 1305,
      "description": "업계 어워드 수상 - 우수 기업으로 선정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1363, 1364, 1365],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.4
    },
    {
      "id": 1306,
      "description": "홍보 효과 제한적 - 투자 대비 효과 미미",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1366, 1367],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.6,
      "stayTimeRangeMax": 15.8
    },
    {
      "id": 1307,
      "description": "긴급 채용 진행 - 경력 인력 즉시 합류",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1368, 1369, 1370],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 5.9,
      "stayTimeRangeMax": 14.7
    },
    {
      "id": 1308,
      "description": "수주 사업 납기 연기 - 여러 거래처 불만 증가",
      "criticalValueMin": 0.5,
      "criticalValueMax": 0.95,
      "next": [1371, 1372],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.1,
      "stayTimeRangeMax": 17.3
    },
    {
      "id": 1309,
      "description": "아웃소싱 활용 - 외부 인력 협력 체계 구축",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1373, 1374],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.7,
      "stayTimeRangeMax": 15.9
    },
    {
      "id": 1310,
      "description": "수주 사업 재개 - 개선된 일정으로 진행",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1375, 1376],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1311,
      "description": "거래처 신뢰 회복 - 추가 기능 개발 수주",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1377, 1378],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.0
    },
    {
      "id": 1312,
      "description": "추가 수주 사업 수주 - 신뢰 회복으로 재계약",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1379, 1380],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 9.8
    },
    {
      "id": 1313,
      "description": "브랜드 가치 상승 - 대형 거래처 유치 유리",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1381, 1382],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.1,
      "stayTimeRangeMax": 13.1
    },
    {
      "id": 1314,
      "description": "비용 최적화 시도 - 운영비 절감 모색",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1383, 1384],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.2,
      "stayTimeRangeMax": 15.8
    },
    {
      "id": 1315,
      "description": "수익성 개선 필요 - 새로운 수익원 발굴",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1385, 1386],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 14.9
    },
    {
      "id": 1316,
      "description": "품질 개선 성과 - 품질 이슈 해소로 신뢰도 회복",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1387, 1388],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.4
    },
    {
      "id": 1317,
      "description": "거래처 만족도 상승 - 추가 수주 예정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1389, 1390],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.6
    },
    {
      "id": 1318,
      "description": "평판 관리 노력 - 긍정적 리뷰 증가",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1391, 1392],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.5,
      "stayTimeRangeMax": 16.1
    },
    {
      "id": 1319,
      "description": "새로운 기회 모색 - 다른 거래처 발굴에 집중",
      "criticalValueMin": 0.75,
      "criticalValueMax": 1.08,
      "next": [1393, 1394],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.8,
      "stayTimeRangeMax": 15.4
    },
    {
      "id": 1320,
      "description": "재기 시도 - 마케팅 강화로 신규 고객 유치",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1395, 1396],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1321,
      "description": "평판 복구 작업 - 고객 서비스 개선",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.1,
      "next": [1397, 1398],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.2,
      "stayTimeRangeMax": 17.0
    },
    {
      "id": 1322,
      "description": "부정적 이미지 지속 - 고객 이탈 계속",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1399, 1801, 1802],
      "nextWeight": [85, 8, 7],
      "stayTimeRangeMin": 9.1,
      "stayTimeRangeMax": 18.3
    },
    {
      "id": 1323,
      "description": "사업장 확장 효과 - 더 많은 인력 수용 가능",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1401, 1402],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.0
    },
    {
      "id": 1324,
      "description": "운영비 부담 증가 - 운영비 상승",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1403, 1404],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1325,
      "description": "프로페셔널 이미지 강화 - 대형 거래처 유치 유리",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1405, 1406],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1326,
      "description": "온라인 마케팅 성과 - 문의 증가",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1407, 1408],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.5
    },
    {
      "id": 1327,
      "description": "마케팅 비용 과다 - ROI 낮음",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1409, 1410],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1328,
      "description": "브랜드 인지도 점진적 상승 - 장기적 효과 기대",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1411, 1412],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1329,
      "description": "개발 생산성 향상 - 도구 활용으로 효율 증가",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1413, 1414],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1330,
      "description": "도구 학습 곡선 - 초기 적응 기간 필요",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.08,
      "next": [1415, 1416],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1331,
      "description": "운영 안정화 - 비용 구조 개선",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1417, 1418],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1332,
      "description": "협상 실패 - 추가 부담 발생",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1419, 1420],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 11.0
    },
    {
      "id": 1333,
      "description": "원격 근무 완전 전환 - 사업장 없이 운영",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.1,
      "next": [1421, 1422],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.5
    },
    {
      "id": 1334,
      "description": "협업 도구 도입 성공 - 원격 근무 효율 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1423, 1424],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1335,
      "description": "도구 비용 부담 - 여러 구독료 누적",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1425, 1426],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1336,
      "description": "원격 근무 문화 정착 - 팀 적응 완료",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1427, 1428],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1337,
      "description": "정기 미팅 도입 - 소통 체계 개선",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1429, 1430],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.5
    },
    {
      "id": 1338,
      "description": "소통 문제 지속 - 원격 근무 한계",
      "criticalValueMin": 0.65,
      "criticalValueMax": 0.9,
      "next": [1431, 1803, 1804],
      "nextWeight": [88, 7, 5],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 11.0
    },
    {
      "id": 1339,
      "description": "수익성 개선 - 운영비 감소로 마진 확대",
      "criticalValueMin": 1,
      "criticalValueMax": 1.09,
      "next": [1432, 1433],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.5
    },
    {
      "id": 1340,
      "description": "비용 절감 한계 - 추가 절감 어려움",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.08,
      "next": [1434, 1435],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1341,
      "description": "MVP 출시 성공 - 초기 사용자 긍정적 반응",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1436, 1437, 1438],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 3.0,
      "stayTimeRangeMax": 6.5
    },
    {
      "id": 1342,
      "description": "사용자 피드백 수집 - 개선점 다수 발견",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1439, 1440],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1343,
      "description": "출시 반응 미미 - 사용자 확보 어려움",
      "criticalValueMin": 0.65,
      "criticalValueMax": 0.9,
      "next": [1441, 1442],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 11.0
    },
    {
      "id": 1344,
      "description": "기술적 해결책 발견 - 난제 극복",
      "criticalValueMin": 1,
      "criticalValueMax": 1.09,
      "next": [1443, 1444],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1345,
      "description": "개발 지연 지속 - 일정 계속 밀림",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1445, 1805, 1806],
      "nextWeight": [88, 7, 5],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 13.0
    },
    {
      "id": 1346,
      "description": "대형 수주 사업 완주 - 업계 명성 확립",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1446, 1447, 1448],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 2.0,
      "stayTimeRangeMax": 4.5
    },
    {
      "id": 1347,
      "description": "추천 네트워크 확장 - 대기업 간 연결",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1449, 1450],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.0,
      "stayTimeRangeMax": 6.0
    },
    {
      "id": 1348,
      "description": "수주 사업 수익성 높음 - 큰 수익 실현",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1451, 1452],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.0
    },
    {
      "id": 1349,
      "description": "대규모 수주 사업 연이어 수주 - 성장 가속화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1701, 1702, 1703],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 2.5,
      "stayTimeRangeMax": 5.5
    },
    {
      "id": 1350,
      "description": "네트워크 효과 - 입소문으로 수주 폭증",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1704, 1705],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.0,
      "stayTimeRangeMax": 6.5
    },
    {
      "id": 1351,
      "description": "수주 사업 선택 필요 - 수주 과다로 선별 필요",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1706, 1707],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1352,
      "description": "프로세스 체계화 - 업무 효율 크게 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1708, 1709],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.5
    },
    {
      "id": 1353,
      "description": "품질 관리 체계 구축 - 표준 프로세스 도입",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1710, 1711],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1354,
      "description": "대형 거래처 유치 - 글로벌 기업과 계약",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1712, 1713, 1714],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.4
    },
    {
      "id": 1355,
      "description": "성공 사례 공유 - 미디어 노출 증가",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1715, 1716],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 14.1
    },
    {
      "id": 1356,
      "description": "마케팅 효과 극대화 - 온라인 문의 폭증",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1717, 1718],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 13.6
    },
    {
      "id": 1357,
      "description": "팀 확장 필요 - 인력 부족 심화",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1719, 1720],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.3,
      "stayTimeRangeMax": 16.1
    },
    {
      "id": 1358,
      "description": "아웃소싱 확대 - 외부 협력 강화",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1721, 1722],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.8,
      "stayTimeRangeMax": 15.2
    },
    {
      "id": 1359,
      "description": "수주 사업 품질 하락 - 거래처 불만 증가",
      "criticalValueMin": 0.65,
      "criticalValueMax": 0.9,
      "next": [1723, 1802, 1803],
      "nextWeight": [87, 8, 5],
      "stayTimeRangeMin": 8.2,
      "stayTimeRangeMax": 17.4
    },
    {
      "id": 1360,
      "description": "전문성 강화 - 특정 분야에서 인정받기 시작",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1724, 1725],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1361,
      "description": "신규 고객 유치 성공 - 문의 증가",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1726, 1727],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 13.4
    },
    {
      "id": 1362,
      "description": "포트폴리오 투자 효과 미미 - ROI 낮음",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1728, 1729],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.1,
      "stayTimeRangeMax": 14.5
    },
    {
      "id": 1363,
      "description": "업계 명성 확립 - 대기업 인정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1730, 1731, 1732],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 11.8
    },
    {
      "id": 1364,
      "description": "인재 채용 유리 - 우수 인력 유치 가능",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1733, 1734],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 12.1
    },
    {
      "id": 1365,
      "description": "어워드 수상 효과 - 홍보 효과 지속",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1735, 1736],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.4
    },
    {
      "id": 1366,
      "description": "마케팅 전략 변경 - 더 효과적인 채널 탐색",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.09,
      "next": [1737, 1738],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1367,
      "description": "마케팅 비용 최적화 - 효율성 개선",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1739, 1740],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1368,
      "description": "팀 역량 강화 - 시너지 효과 발생",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1741, 1742],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.0
    },
    {
      "id": 1369,
      "description": "팀 통합 어려움 - 문화 차이 발생",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1743, 1744],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1370,
      "description": "인력 확보 성공 - 업무 분산 가능",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1745, 1746],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1371,
      "description": "거래처 이탈 - 신뢰도 회복 어려움",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1747, 1803, 1804],
      "nextWeight": [88, 7, 5],
      "stayTimeRangeMin": 8.5,
      "stayTimeRangeMax": 17.7
    },
    {
      "id": 1372,
      "description": "일정 재조정 성공 - 수주 사업 재개",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1748, 1749],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.2,
      "stayTimeRangeMax": 16.4
    },
    {
      "id": 1373,
      "description": "협력 체계 정착 - 효율성 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1750, 1751],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.1,
      "stayTimeRangeMax": 14.3
    },
    {
      "id": 1374,
      "description": "아웃소싱 비용 증가 - 수익성 악화",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1752, 1753],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.5,
      "stayTimeRangeMax": 16.5
    },
    {
      "id": 1701,
      "description": "글로벌 기업과 전략적 제휴 - 해외 진출 기반 마련",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1754, 1755, 1756],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1702,
      "description": "IPO 준비 착수 - 상장 준비 시작",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1757, 1758, 1759],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1703,
      "description": "대규모 투자 유치 - 벤처캐피털 투자 성공",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1760, 1761],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1704,
      "description": "시장 점유율 확대 - 업계 1위 달성",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1762, 1763],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1705,
      "description": "브랜드 가치 최고치 - 기업가치 평가 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1764, 1765],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.3,
      "stayTimeRangeMax": 10.7
    },
    {
      "id": 1706,
      "description": "수주 사업 포트폴리오 최적화 - 고수익 수주 사업 선별",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1766, 1767],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1707,
      "description": "수주 사업 관리 시스템 고도화 - AI 도입",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1768, 1769],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1708,
      "description": "자동화 시스템 완성 - 개발 효율 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1770, 1771],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1709,
      "description": "업무 프로세스 표준화 - 품질 균일화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1772, 1773],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.3,
      "stayTimeRangeMax": 10.7
    },
    {
      "id": 1710,
      "description": "품질 관리 체계 완성 - 고품질 보장",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1774, 1775],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.1,
      "stayTimeRangeMax": 10.5
    },
    {
      "id": 1711,
      "description": "표준화 작업 부담 - 초기 비용 증가",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1776, 1777],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 12.8
    },
    {
      "id": 1712,
      "description": "글로벌 수주 사업 수주 - 해외 시장 진출",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1778, 1779],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1713,
      "description": "국제 경쟁력 확보 - 글로벌 인정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1780, 1781],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1714,
      "description": "해외 진출 리스크 - 환율 및 문화 차이",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1782, 1783],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 14.5
    },
    {
      "id": 1715,
      "description": "미디어 노출 효과 - 브랜드 인지도 급상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1784, 1785],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1716,
      "description": "홍보 효과 지속 - 장기 브랜드 가치 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1786, 1787],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1717,
      "description": "온라인 문의 폭증 - 영업 인력 부족",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1788, 1789],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.1,
      "stayTimeRangeMax": 13.1
    },
    {
      "id": 1718,
      "description": "영업 팀 확장 - 체계적 고객 관리",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1790, 1791],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.7,
      "stayTimeRangeMax": 12.3
    },
    {
      "id": 1719,
      "description": "인력 충원 성공 - 업무 분담 개선",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1792, 1793],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.3,
      "stayTimeRangeMax": 13.7
    },
    {
      "id": 1720,
      "description": "채용 경쟁 심화 - 인건비 부담 증가",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1794, 1795],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1721,
      "description": "외부 협력 체계 안정화 - 효율성 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1796, 1797],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1722,
      "description": "협력 업체 관리 부담 - 품질 통제 어려움",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1798, 1801, 1805],
      "nextWeight": [88, 7, 5],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.8
    },
    {
      "id": 1723,
      "description": "고객 이탈 가속화 - 수익 급감",
      "criticalValueMin": 0.55,
      "criticalValueMax": 0.7,
      "next": [1801, 1802, 1999, 1998, 1997],
      "nextWeight": [85, 85, 1, 2, 5],
      "stayTimeRangeMin": 9.5,
      "stayTimeRangeMax": 19.5
    },
    {
      "id": 1375,
      "description": "수주 사업 재개 성공 - 일정 준수로 신뢰 회복",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1301, 1302],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 13.4
    },
    {
      "id": 1376,
      "description": "수주 사업 재개 지연 - 추가 문제 발생",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1303, 1304],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1377,
      "description": "추가 기능 개발 성공 - 거래처 만족",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1305, 1306],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1378,
      "description": "추가 기능 개발 지연 - 일정 초과",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1307, 1308],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.5,
      "stayTimeRangeMax": 16.5
    },
    {
      "id": 1379,
      "description": "재계약 성공 - 지속적 협력 관계",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1309, 1310],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1380,
      "description": "재계약 실패 - 관계 악화",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1311, 1312],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.2,
      "stayTimeRangeMax": 17.4
    },
    {
      "id": 1381,
      "description": "대형 거래처 유치 성공 - 브랜드 가치 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1313, 1314],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1382,
      "description": "대형 거래처 유치 실패 - 경쟁에서 밀림",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1315, 1316],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.8
    },
    {
      "id": 1383,
      "description": "비용 최적화 성공 - 운영비 감소",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1317, 1318],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.8,
      "stayTimeRangeMax": 14.2
    },
    {
      "id": 1384,
      "description": "비용 최적화 실패 - 추가 비용 발생",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1319, 1320],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.1,
      "stayTimeRangeMax": 17.3
    },
    {
      "id": 1385,
      "description": "새로운 수익원 발굴 성공 - 성장 동력 확보",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1321, 1322],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1386,
      "description": "수익원 발굴 실패 - 투자만 손실",
      "criticalValueMin": 0.65,
      "criticalValueMax": 0.9,
      "next": [1323, 1324],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.5,
      "stayTimeRangeMax": 17.7
    },
    {
      "id": 1387,
      "description": "품질 개선 지속 - 신뢰도 회복 중",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1701, 1702],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1388,
      "description": "거래처 만족도 상승 - 재계약 확정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1703, 1704],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1389,
      "description": "추가 수주 사업 수주 - 성장 기반 마련",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1705, 1706],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.1,
      "stayTimeRangeMax": 13.1
    },
    {
      "id": 1390,
      "description": "브랜드 가치 상승 - 대형 거래처 유치",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1707, 1708],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.9,
      "stayTimeRangeMax": 12.5
    },
    {
      "id": 1391,
      "description": "평판 복구 성공 - 긍정적 전환",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1709, 1710],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.6
    },
    {
      "id": 1392,
      "description": "평판 관리 지속 필요 - 장기 노력",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1711, 1712],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1393,
      "description": "새로운 거래처 발견 - 성장 기회",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1713, 1714],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.4,
      "stayTimeRangeMax": 13.4
    },
    {
      "id": 1394,
      "description": "마케팅 노력 계속 - 점진적 개선",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1715, 1716],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1395,
      "description": "재기 성공 - 새로운 전환점",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1717, 1718],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.7,
      "stayTimeRangeMax": 13.7
    },
    {
      "id": 1396,
      "description": "재기 어려움 - 시장 경쟁 심화",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1719, 1806, 1807],
      "nextWeight": [88, 7, 5],
      "stayTimeRangeMin": 8.1,
      "stayTimeRangeMax": 17.3
    },
    {
      "id": 1397,
      "description": "평판 회복 중 - 점진적 개선",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.09,
      "next": [1720, 1721],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.4
    },
    {
      "id": 1398,
      "description": "고객 서비스 개선 효과 - 신뢰 회복",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1722, 1723],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.9,
      "stayTimeRangeMax": 15.5
    },
    {
      "id": 1399,
      "description": "부정적 이미지 지속 - 고객 이탈 가속",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1801, 1802, 1999, 1998, 1997],
      "nextWeight": [65, 65, 1, 2, 5],
      "stayTimeRangeMin": 9.5,
      "stayTimeRangeMax": 19.5
    },
    {
      "id": 1401,
      "description": "사업장 확장 효과 - 더 많은 인력 수용",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1301, 1302],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.0
    },
    {
      "id": 1402,
      "description": "운영비 부담 증가 - 운영비 상승",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1303, 1304],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1403,
      "description": "프로페셔널 이미지 강화 - 대형 거래처 유치",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1305, 1306],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1404,
      "description": "운영비 부담 지속 - 수익성 악화",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1307, 1308],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1405,
      "description": "대형 거래처 유치 성공 - 브랜드 가치 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1309, 1310],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1406,
      "description": "대형 거래처 관리 부담 - 리스크 증가",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1311, 1312],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.8,
      "stayTimeRangeMax": 15.2
    },
    {
      "id": 1407,
      "description": "온라인 마케팅 성과 - 문의 증가",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1313, 1314],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.5
    },
    {
      "id": 1408,
      "description": "마케팅 비용 과다 - ROI 낮음",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1315, 1316],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1409,
      "description": "브랜드 인지도 점진적 상승 - 장기 효과",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1317, 1318],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1410,
      "description": "마케팅 전략 재검토 - 효과 미미",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1319, 1320],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.1,
      "stayTimeRangeMax": 14.5
    },
    {
      "id": 1411,
      "description": "개발 생산성 향상 - 도구 활용 성공",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1321, 1322],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1412,
      "description": "도구 적응 완료 - 효율성 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1323, 1324],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 9.8
    },
    {
      "id": 1413,
      "description": "도구 학습 곡선 - 초기 적응 기간",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.08,
      "next": [1325, 1326],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1414,
      "description": "도구 비용 부담 - 구독료 누적",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1327, 1328],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1415,
      "description": "운영 안정화 - 비용 구조 개선",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1329, 1330],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1416,
      "description": "협상 실패 - 추가 부담",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1331, 1332],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 11.0
    },
    {
      "id": 1417,
      "description": "원격 근무 완전 전환 - 사업장 없이 운영",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.1,
      "next": [1333, 1334],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.5
    },
    {
      "id": 1418,
      "description": "이미지 하락 - 사업장 없음으로 신뢰도 감소",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1335, 1803],
      "nextWeight": [93, 7],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 11.0
    },
    {
      "id": 1419,
      "description": "협업 도구 도입 성공 - 원격 근무 효율 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1336, 1337],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1420,
      "description": "도구 비용 부담 - 여러 구독료 누적",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1338, 1339],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1421,
      "description": "원격 근무 문화 정착 - 팀 적응 완료",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1340, 1341],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1422,
      "description": "비용 절감 효과 - 운영비 최소화",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1342, 1343],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.5
    },
    {
      "id": 1423,
      "description": "정기 미팅 도입 - 소통 체계 개선",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1344, 1345],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.5
    },
    {
      "id": 1424,
      "description": "소통 문제 지속 - 원격 근무 한계",
      "criticalValueMin": 0.65,
      "criticalValueMax": 0.9,
      "next": [1346, 1804],
      "nextWeight": [93, 7],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 11.0
    },
    {
      "id": 1425,
      "description": "수익성 개선 - 운영비 감소로 마진 확대",
      "criticalValueMin": 1,
      "criticalValueMax": 1.09,
      "next": [1347, 1348],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.5
    },
    {
      "id": 1426,
      "description": "비용 절감 한계 - 추가 절감 어려움",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.08,
      "next": [1349, 1350],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1427,
      "description": "재투자 결정 - 절감된 비용으로 마케팅 확대",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1351, 1352],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1428,
      "description": "재투자 효과 미미 - ROI 낮음",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1353, 1354],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.1,
      "stayTimeRangeMax": 14.5
    },
    {
      "id": 1429,
      "description": "하이브리드 모델 검토 - 사업장 + 재택 병행",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.1,
      "next": [1355, 1356],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1430,
      "description": "하이브리드 모델 실패 - 비용 증가만 발생",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1357, 1358],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1431,
      "description": "원격 근무 포기 - 사업장 재개",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1359, 1360],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1432,
      "description": "재투자 성공 - 마케팅 효과 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1361, 1362],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1433,
      "description": "재투자 효과 제한적 - 기대치 미달",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1363, 1364],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1434,
      "description": "비용 최적화 모색 - 새로운 방법 탐색",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1365, 1366],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.8,
      "stayTimeRangeMax": 15.2
    },
    {
      "id": 1435,
      "description": "비용 절감 포기 - 현상 유지",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1367, 1368],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1436,
      "description": "MVP 출시 성공 - 초기 사용자 긍정적 반응",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1369, 1370],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.0,
      "stayTimeRangeMax": 6.5
    },
    {
      "id": 1437,
      "description": "사용자 피드백 수집 - 개선점 발견",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1371, 1372],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1438,
      "description": "출시 반응 미미 - 사용자 확보 어려움",
      "criticalValueMin": 0.65,
      "criticalValueMax": 0.9,
      "next": [1373, 1374],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 11.0
    },
    {
      "id": 1439,
      "description": "피드백 반영 성공 - 사용자 만족도 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1701, 1702],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 13.4
    },
    {
      "id": 1440,
      "description": "개선 작업 부담 - 리소스 부족",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1703, 1704],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1441,
      "description": "마케팅 강화 - 사용자 확보 노력",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1705, 1706],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1442,
      "description": "서비스 중단 검토 - 실패 인정",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1707, 1805],
      "nextWeight": [93, 7],
      "stayTimeRangeMin": 8.2,
      "stayTimeRangeMax": 17.4
    },
    {
      "id": 1443,
      "description": "기술적 해결책 발견 - 난제 극복",
      "criticalValueMin": 1,
      "criticalValueMax": 1.09,
      "next": [1708, 1709],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 8.0
    },
    {
      "id": 1444,
      "description": "기술적 해결책 부분 성공 - 일부 문제 남음",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1710, 1711],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1445,
      "description": "개발 포기 결정 - 수주 사업 중단",
      "criticalValueMin": 0.55,
      "criticalValueMax": 0.7,
      "next": [1801, 1802, 1999, 1998, 1997],
      "nextWeight": [25, 25, 1, 2, 5],
      "stayTimeRangeMin": 9.0,
      "stayTimeRangeMax": 18.0
    },
    {
      "id": 1446,
      "description": "업계 명성 확립 - 대기업 인정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1712, 1713],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 8.5
    },
    {
      "id": 1447,
      "description": "추천 네트워크 확장 - 대기업 간 연결",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1714, 1715],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1448,
      "description": "수주 사업 수익성 높음 - 큰 수익 실현",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1716, 1717],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 10.5
    },
    {
      "id": 1449,
      "description": "대규모 수주 사업 연이어 수주 - 성장 가속화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1718, 1719],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 8.5
    },
    {
      "id": 1450,
      "description": "네트워크 효과 - 입소문으로 수주 폭증",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1720, 1721],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1451,
      "description": "수주 사업 선택 필요 - 수주 과다로 선별",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1722, 1723],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 11.2
    },
    {
      "id": 1452,
      "description": "수익 극대화 - 효율적 수주 사업 관리",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1724, 1725],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.2
    },
    {
      "id": 1724,
      "description": "전문성 강화 - 특정 분야 인정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1701, 1702],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 9.0
    },
    {
      "id": 1725,
      "description": "신규 고객 유치 성공 - 문의 증가",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1703, 1704],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 13.4
    },
    {
      "id": 1726,
      "description": "포트폴리오 투자 효과 미미 - ROI 낮음",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1705, 1706],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.1,
      "stayTimeRangeMax": 14.5
    },
    {
      "id": 1727,
      "description": "업계 명성 확립 - 대기업 인정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1707, 1708],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 11.8
    },
    {
      "id": 1728,
      "description": "인재 채용 유리 - 우수 인력 유치",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1709, 1710],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 12.1
    },
    {
      "id": 1729,
      "description": "어워드 수상 효과 - 홍보 효과 지속",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1711, 1712],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.4
    },
    {
      "id": 1730,
      "description": "마케팅 전략 변경 - 효과적 채널 탐색",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.09,
      "next": [1713, 1714],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1731,
      "description": "마케팅 비용 최적화 - 효율성 개선",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1715, 1716],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1732,
      "description": "팀 역량 강화 - 시너지 효과",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1717, 1718],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 3.5,
      "stayTimeRangeMax": 7.0
    },
    {
      "id": 1733,
      "description": "팀 통합 어려움 - 문화 차이",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1719, 1720],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1734,
      "description": "인력 확보 성공 - 업무 분산",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1721, 1722],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1735,
      "description": "거래처 이탈 - 신뢰도 회복 어려움",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1723, 1802, 1803],
      "nextWeight": [87, 8, 5],
      "stayTimeRangeMin": 8.5,
      "stayTimeRangeMax": 17.7
    },
    {
      "id": 1736,
      "description": "일정 재조정 성공 - 수주 사업 재개",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1701, 1702],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.2,
      "stayTimeRangeMax": 16.4
    },
    {
      "id": 1737,
      "description": "협력 체계 정착 - 효율성 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1703, 1704],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.1,
      "stayTimeRangeMax": 14.3
    },
    {
      "id": 1738,
      "description": "아웃소싱 비용 증가 - 수익성 악화",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1705, 1706],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.5,
      "stayTimeRangeMax": 16.5
    },
    {
      "id": 1739,
      "description": "글로벌 기업과 전략적 제휴 - 해외 진출",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1707, 1708],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1740,
      "description": "IPO 준비 착수 - 상장 준비",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1709, 1710],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1741,
      "description": "대규모 투자 유치 - 벤처캐피털 투자",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1711, 1712],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1742,
      "description": "시장 점유율 확대 - 업계 1위",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1713, 1714],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1743,
      "description": "브랜드 가치 최고치 - 기업가치 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1715, 1716],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.3,
      "stayTimeRangeMax": 10.7
    },
    {
      "id": 1744,
      "description": "수주 사업 포트폴리오 최적화 - 고수익 선별",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1717, 1718],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1745,
      "description": "수주 사업 관리 시스템 고도화 - AI 도입",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1719, 1720],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1746,
      "description": "자동화 시스템 완성 - 효율 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1721, 1722],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.0,
      "stayTimeRangeMax": 10.0
    },
    {
      "id": 1747,
      "description": "업무 프로세스 표준화 - 품질 균일화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1723, 1802, 1803],
      "nextWeight": [87, 8, 5],
      "stayTimeRangeMin": 4.3,
      "stayTimeRangeMax": 10.7
    },
    {
      "id": 1748,
      "description": "품질 관리 체계 완성 - 고품질 보장",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1701, 1702],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.1,
      "stayTimeRangeMax": 10.5
    },
    {
      "id": 1749,
      "description": "표준화 작업 부담 - 초기 비용 증가",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1703, 1704],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 12.8
    },
    {
      "id": 1750,
      "description": "글로벌 수주 사업 수주 - 해외 시장 진출",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1705, 1706],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1751,
      "description": "국제 경쟁력 확보 - 글로벌 인정",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1707, 1708],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1752,
      "description": "해외 진출 리스크 - 환율 및 문화 차이",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1709, 1710],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 14.5
    },
    {
      "id": 1753,
      "description": "미디어 노출 효과 - 브랜드 인지도 급상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1711, 1712],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1754,
      "description": "홍보 효과 지속 - 장기 브랜드 가치 상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1713, 1714],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1755,
      "description": "온라인 문의 폭증 - 영업 인력 부족",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1715, 1716],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.1,
      "stayTimeRangeMax": 13.1
    },
    {
      "id": 1756,
      "description": "영업 팀 확장 - 체계적 고객 관리",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1717, 1718],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.7,
      "stayTimeRangeMax": 12.3
    },
    {
      "id": 1757,
      "description": "인력 충원 성공 - 업무 분담 개선",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1719, 1720],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.3,
      "stayTimeRangeMax": 13.7
    },
    {
      "id": 1758,
      "description": "채용 경쟁 심화 - 인건비 부담 증가",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1721, 1722],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1759,
      "description": "외부 협력 체계 안정화 - 효율성 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1723, 1802, 1803],
      "nextWeight": [87, 8, 5],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1760,
      "description": "협력 업체 관리 부담 - 품질 통제 어려움",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1701, 1806],
      "nextWeight": [93, 7],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.8
    },
    {
      "id": 1761,
      "description": "고객 이탈 가속화 - 수익 급감",
      "criticalValueMin": 0.55,
      "criticalValueMax": 0.7,
      "next": [1801, 1802, 1999, 1998, 1997],
      "nextWeight": [65, 65, 1, 2, 5],
      "stayTimeRangeMin": 9.5,
      "stayTimeRangeMax": 19.5
    },
    {
      "id": 1762,
      "description": "시장 점유율 유지 - 안정적 성장",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1702, 1703],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1763,
      "description": "경쟁 심화 - 시장 점유율 하락",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.09,
      "next": [1704, 1705],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.5
    },
    {
      "id": 1764,
      "description": "브랜드 가치 유지 - 높은 평가 지속",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1706, 1707],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1765,
      "description": "브랜드 가치 하락 - 평가 절하",
      "criticalValueMin": 0.9,
      "criticalValueMax": 1.1,
      "next": [1708, 1709],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1766,
      "description": "포트폴리오 최적화 성공 - 수익성 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1710, 1711],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1767,
      "description": "선별 기준 실패 - 좋은 수주 사업 놓침",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1712, 1713],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.5,
      "stayTimeRangeMax": 15.1
    },
    {
      "id": 1768,
      "description": "AI 시스템 도입 성공 - 효율성 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1714, 1715],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1769,
      "description": "AI 시스템 도입 실패 - 비용만 증가",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1716, 1717],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.1,
      "stayTimeRangeMax": 16.3
    },
    {
      "id": 1770,
      "description": "자동화 성공 - 개발 효율 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1718, 1719],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1771,
      "description": "자동화 투자 과다 - ROI 낮음",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1720, 1721],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.8,
      "stayTimeRangeMax": 15.2
    },
    {
      "id": 1772,
      "description": "표준화 성공 - 품질 균일화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1722, 1723],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1773,
      "description": "표준화 저항 - 팀 적응 어려움",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1801, 1802, 1999, 1998, 1997],
      "nextWeight": [115, 115, 1, 2, 5],
      "stayTimeRangeMin": 7.5,
      "stayTimeRangeMax": 16.5
    },
    {
      "id": 1774,
      "description": "품질 관리 완성 - 고품질 보장",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1701, 1702],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.3,
      "stayTimeRangeMax": 10.7
    },
    {
      "id": 1775,
      "description": "품질 관리 부담 - 비용 증가",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1703, 1704],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1776,
      "description": "표준화 투자 성공 - 장기 이익",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1705, 1706],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1777,
      "description": "표준화 비용 과다 - 수익성 악화",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1707, 1708],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.8
    },
    {
      "id": 1778,
      "description": "해외 수주 사업 성공 - 글로벌 진출 성공",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1709, 1710],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1779,
      "description": "해외 수주 사업 실패 - 문화 차이 문제",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1711, 1712],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.2,
      "stayTimeRangeMax": 17.4
    },
    {
      "id": 1780,
      "description": "국제 인정 확보 - 글로벌 시장 진입",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1713, 1714],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1781,
      "description": "글로벌 경쟁 실패 - 해외 철수",
      "criticalValueMin": 0.65,
      "criticalValueMax": 0.9,
      "next": [1715, 1716],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 8.5,
      "stayTimeRangeMax": 17.7
    },
    {
      "id": 1782,
      "description": "해외 진출 리스크 관리 - 점진적 성장",
      "criticalValueMin": 1,
      "criticalValueMax": 1.08,
      "next": [1717, 1718],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.8,
      "stayTimeRangeMax": 15.2
    },
    {
      "id": 1783,
      "description": "해외 진출 실패 - 큰 손실",
      "criticalValueMin": 0.6,
      "criticalValueMax": 0.8,
      "next": [1719, 1806, 1807],
      "nextWeight": [88, 7, 5],
      "stayTimeRangeMin": 9.2,
      "stayTimeRangeMax": 18.4
    },
    {
      "id": 1784,
      "description": "미디어 노출 효과 - 브랜드 인지도 급상승",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1720, 1721],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1785,
      "description": "홍보 효과 지속 - 장기 브랜드 가치",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1722, 1723],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.1,
      "stayTimeRangeMax": 13.1
    },
    {
      "id": 1786,
      "description": "온라인 문의 폭증 - 영업 인력 부족",
      "criticalValueMin": 0.85,
      "criticalValueMax": 0.95,
      "next": [1801, 1802, 1999, 1998, 1997],
      "nextWeight": [215, 215, 1, 2, 5],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1787,
      "description": "영업 팀 확장 - 체계적 고객 관리",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1701, 1702],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 12.0
    },
    {
      "id": 1788,
      "description": "인력 충원 성공 - 업무 분담 개선",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1703, 1704],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1789,
      "description": "채용 경쟁 심화 - 인건비 부담",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1705, 1706],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.5,
      "stayTimeRangeMax": 16.5
    },
    {
      "id": 1790,
      "description": "외부 협력 체계 안정화 - 효율성 극대화",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1707, 1708],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.8,
      "stayTimeRangeMax": 13.8
    },
    {
      "id": 1791,
      "description": "협력 업체 관리 부담 - 품질 통제 어려움",
      "criticalValueMin": 0.8,
      "criticalValueMax": 1.1,
      "next": [1709, 1807],
      "nextWeight": [93, 7],
      "stayTimeRangeMin": 8.1,
      "stayTimeRangeMax": 17.3
    },
    {
      "id": 1792,
      "description": "인력 확보 성공 - 업무 효율 향상",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1710, 1711],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1793,
      "description": "인건비 부담 증가 - 수익성 악화",
      "criticalValueMin": 0.7,
      "criticalValueMax": 1.08,
      "next": [1712, 1713],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 7.8,
      "stayTimeRangeMax": 16.8
    },
    {
      "id": 1794,
      "description": "인건비 최적화 성공 - 효율적 운영",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1714, 1715],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 6.2,
      "stayTimeRangeMax": 14.8
    },
    {
      "id": 1795,
      "description": "인건비 부담 지속 - 현금 흐름 악화",
      "criticalValueMin": 0.6,
      "criticalValueMax": 1.1,
      "next": [1716, 1801],
      "nextWeight": [93, 7],
      "stayTimeRangeMin": 8.5,
      "stayTimeRangeMax": 17.7
    },
    {
      "id": 1796,
      "description": "협력 체계 안정화 - 장기 성장 기반",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1717, 1718],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.8,
      "stayTimeRangeMax": 13.8
    },
    {
      "id": 1797,
      "description": "협력 업체 이탈 - 품질 문제 발생",
      "criticalValueMin": 0.5,
      "criticalValueMax": 1,
      "next": [1719, 1806, 1807],
      "nextWeight": [88, 7, 5],
      "stayTimeRangeMin": 8.2,
      "stayTimeRangeMax": 17.4
    },
    {
      "id": 1798,
      "description": "협력 체계 붕괴 - 큰 손실",
      "criticalValueMin": 0.55,
      "criticalValueMax": 0.7,
      "next": [1801, 1802, 1999, 1998, 1997],
      "nextWeight": [35, 35, 1, 2, 5],
      "stayTimeRangeMin": 9.5,
      "stayTimeRangeMax": 19.5
    },
    {
      "id": 1801,
      "description": "파산 직전 긴급 투자 유치 성공 - 기적의 투자자 등장",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1701, 1702, 1703],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1802,
      "description": "기적의 계약 - 대형 거래처가 위기 회사에 손 내밀어",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1704, 1705, 1706],
      "nextWeight": [35, 35, 30],
      "stayTimeRangeMin": 4.2,
      "stayTimeRangeMax": 10.8
    },
    {
      "id": 1803,
      "description": "놀라운 합병 제안 - 경쟁사가 인수 제안으로 구원",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1707, 1708],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.0,
      "stayTimeRangeMax": 12.0
    },
    {
      "id": 1804,
      "description": "전 직원 재결합 - 떠났던 핵심 인력들이 돌아와 재건",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1709, 1710],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.8,
      "stayTimeRangeMax": 12.2
    },
    {
      "id": 1805,
      "description": "새로운 기술로 시장 재진입 - 혁신적 아이디어로 재기",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1711, 1712],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.2,
      "stayTimeRangeMax": 13.4
    },
    {
      "id": 1806,
      "description": "정부 지원금 획득 - 정부 정책 덕분에 생존",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.09,
      "next": [1713, 1714],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 5.5,
      "stayTimeRangeMax": 13.5
    },
    {
      "id": 1807,
      "description": "예상치 못한 수익 발생 - 과거 투자가 열매 맺어",
      "criticalValueMin": 1.05,
      "criticalValueMax": 1.1,
      "next": [1715, 1716],
      "nextWeight": [60, 40],
      "stayTimeRangeMin": 4.5,
      "stayTimeRangeMax": 11.5
    },
    {
      "id": 1997,
      "description": "상장폐지 확정 - 거래소 상장폐지 처분",
      "criticalValueMin": 0.001,
      "criticalValueMax": 0.02,
      "next": [],
      "nextWeight": [],
      "stayTimeRangeMin": 0.0,
      "stayTimeRangeMax": 0.0
    },
    {
      "id": 1998,
      "description": "상장폐지 확정 - 상장 유지 요건 미충족",
      "criticalValueMin": 0.05,
      "criticalValueMax": 0.1,
      "next": [],
      "nextWeight": [],
      "stayTimeRangeMin": 0.0,
      "stayTimeRangeMax": 0.0
    },
    {
      "id": 1999,
      "description": "상장폐지 확정 - 종목 명의개서 제한",
      "criticalValueMin": 0.1,
      "criticalValueMax": 0.4,
      "next": [],
      "nextWeight": [],
      "stayTimeRangeMin": 0.0,
      "stayTimeRangeMax": 0.0
    }
  ]
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { itCompanyData };
}