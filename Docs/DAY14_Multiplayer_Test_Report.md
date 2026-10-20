# DAY 14 멀티플레이 통합 테스트 기록

## 테스트 환경

- Unity: 6000.6.5f1
- Scene: `Assets/Scenes/NetworkPlay.unity`
- Netcode for GameObjects: 2.13.3
- Multiplayer Play Mode: 3.0.0
- Unity Transport: 2.6.0
- 대상: Host 1 + Client 1
- 주소: `127.0.0.1:7777`

> 실제 결과는 화면과 Console에서 확인한 뒤 기록한다.
> 실패한 항목은 재현 절차를 반드시 남긴다.

## Host / Client 통합 테스트표

| 번호 | 절차 | 기대 결과 | 실제 결과 | 판정 |
| :--- | :--- | :--- | :--- | :--- |
| 1 | Host 시작 | Host Player가 생성된다. | Host Player 생성 확인 | 통과 |
| 2 | Client 연결 | 양쪽에 Player 2개가 보인다. | Host/Client 양쪽에서 Player 2개 확인 | 통과 |
| 3 | 코인 획득 | 코인이 양쪽에서 한 번 사라지고 점수가 동기화된다. | 코인이 양쪽에서 한 번 사라지고 점수 동기화 확인 | 통과 |
| 4 | Client 종료 | Host Console에 `ClientDisconnected`가 기록되고 남은 Player가 유지된다. | Client 연결 해제 후 Host Player 유지 및 종료 처리 확인 | 통과 |
| 5 | 다시 연결 | 새 Player가 생성되고 현재 공유 상태를 받는다. | 같은 Virtual Player에서 Client 재접속 성공 | 통과 |

## 연결 로그 확인 기준

정상 예시:

```text
2026-10-20T12:00:00.0000000+09:00 HostStarted ClientId=0
2026-10-20T12:00:05.0000000+09:00 ClientConnected ClientId=1
2026-10-20T12:01:00.0000000+09:00 ClientDisconnected ClientId=1
```

## 실패 기록 양식

### 이슈 번호

- 실패 항목:
- 재현 절차:
  1.
  2.
  3.
- Console 메시지:
- 기대 결과:
- 실제 결과:
- 예상 원인:
- 수정:
- 다시 확인:
- 최종 판정:

## 요청 수 비교

DAY07의 10회 / 100회 요청 테스트를 동일 조건으로 다시 실행한다.

| 조건 | 요청 수 | 성공 수 | 오류 수 | 총 시간 | 비고 |
| :--- | ---: | ---: | ---: | ---: | :--- |
| 변경 전 | 10 | 10 | 0 | 14.45 ms | ClientId=1, Timeout=False |
| 변경 전 | 100 | 100 | 0 | 12.12 ms | ClientId=1, Timeout=False |
| 변경 후 | 10 | 10 | 0 | 27.21 ms | Batch10, 실제 송신 RPC 1회, Timeout=False |
| 변경 후 | 100 | 100 | 0 | 15.94 ms | Batch10, 실제 송신 RPC 10회, Timeout=False |

## 결론

이번 1회 측정에서는 모든 조건에서 성공률 100%, 오류 0건을 기록했다.

- 10회: 개별 RPC 10회 / 14.45 ms → Batch10 RPC 1회 / 27.21 ms
- 100회: 개별 RPC 100회 / 12.12 ms → Batch10 RPC 10회 / 15.94 ms
- Batch10은 실제 송신 RPC 수를 90% 줄였다.
- 그러나 이번 로컬 MPPM 단일 측정에서는 총 시간은 개선되지 않았다.
- 따라서 "배칭이 더 빠르다"라고 결론 내릴 수 없으며, 반복 측정 평균과 실제 빌드/네트워크 환경 검증이 추가로 필요하다.

## 완료 확인

- [x] Host와 Client 양쪽 결과를 기록했다.
- [x] 한 번 이상 연결 종료·재연결을 시험했다.
- [x] 실패가 있으면 재현 절차를 남겼다. (현재 실패 항목 없음)
- [x] 10회 / 100회 요청 비교값을 기록했다.
