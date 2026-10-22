# GameNetworkLab 제출 문서 예시 - TEST_REPORT

> 이 문서는 예시입니다.
>
> 이미 실제로 확인한 DAY14 결과는 기록되어 있습니다.
> DAY15 최종 기능과 UDP 최종 결과는 사용자 환경에서 다시 실행한 뒤 `[실행 후 기입]` 부분을 실제 값으로 바꿉니다.

## 1. 시험 환경

- Unity: `6000.6.5f1`
- Scene: `Assets/Scenes/NetworkPlay.unity`
- NGO: `2.13.3`
- Multiplayer Play Mode: `3.0.0`
- NGO Address: `127.0.0.1:7777`
- UDP Target Framework: `net10.0`
- UDP Peer A: `127.0.0.1:9001`
- UDP Peer B: `127.0.0.1:9002`

## 2. NGO 기본 연결 시험

### 시험 목적

Host와 Client가 연결되고 각 Player NetworkObject가 생성되는지 확인합니다.

### 재현 절차

1. Host 창에서 Host 시작
2. Client 창에서 Client 시작
3. 양쪽 Hierarchy/게임 화면/Console 확인

### 기대 결과

- Host가 계속 실행된다.
- Client가 연결된다.
- 양쪽에 두 플레이어가 존재한다.
- 각 사용자는 자신의 플레이어만 조작한다.

### 실제 결과

DAY14 확인 결과:

- Host Player 생성: 통과
- Client 연결: 통과
- 양쪽 Player 2개 확인: 통과

### 판정

통과

---

## 3. CharacterController 이동 시험

### 시험 목적

플레이어가 `CharacterController.Move()`를 통해 실제 이동하는지 확인합니다.

### 재현 절차

1. Host와 Client 연결
2. 각 창에서 이동 입력
3. 자신의 Player만 이동하는지 확인

### 기대 결과

- 자기 Player만 입력에 반응
- 다른 Client Player를 직접 조작할 수 없음

### 실제 결과

`[최종 실행 후 기입]`

### 판정

`[기입]`

---

## 4. 정상 코인 획득 시험

### 시험 목적

정상 Coin 요청이 Server에서 승인되고 상태가 양쪽에 동기화되는지 확인합니다.

### 재현 절차

1. 플레이어를 Coin에서 2.0 이내로 이동
2. 시험 패널의 `가까운 코인 정상 요청` 실행
3. 양쪽 점수/코인 상태 확인

### 기대 결과

- Server 승인 로그
- 요청한 Player 점수 +1
- Coin 제거
- Host/Client에서 동일 상태 확인

### 실제 결과

DAY14 코인 시험에서는 코인 제거 및 점수 동기화를 확인했습니다.

DAY15 명시적 정상 요청 시험 결과:

`[최종 실행 후 기입]`

### 판정

`[기입]`

---

## 5. 잘못된 Coin ID 시험

### 재현 절차

`잘못된 Coin ID 요청` 버튼 실행

### 기대 결과

- Server가 ID를 거절
- 점수 변화 없음
- 게임 계속 실행

### 실제 결과

`[실행 후 Console 로그 및 점수 기입]`

### 판정

`[기입]`

---

## 6. 거리 초과 Coin 요청 시험

### 재현 절차

1. 남아 있는 Coin에서 2.0보다 멀리 이동
2. `너무 먼 Coin 요청` 실행

### 기대 결과

- Server 거리 검사 실패
- 점수 변화 없음
- Coin 유지

### 실제 결과

`[실행 후 기입]`

### 판정

`[기입]`

---

## 7. 중복 Coin 요청 시험

### 재현 절차

1. Coin 2.0 이내로 이동
2. `가까운 코인에 2회 동시 요청` 실행

### 기대 결과

- 요청 2회
- 보상은 1회만 적용
- Score Delta = 1
- Coin은 한 번만 제거

### 실제 결과

`[DAY15 DuplicateCoinTestResult 로그 기입]`

### 판정

`[기입]`

---

## 8. 공동 목표 시험

### 재현 절차

1. 총 3개의 Coin 획득
2. Host와 Client UI 확인

### 기대 결과

양쪽 모두:

```text
게임 성공
공동 코인: 3 / 3
```

성공 처리는 한 번만 수행됩니다.

### 실제 결과

`[실행 후 기입]`

### 판정

`[기입]`

---

## 9. Client 종료 / Host 유지 / 재접속 시험

### 재현 절차

1. Host + Client 연결
2. 일부 Coin을 획득
3. Client Disconnect
4. Host가 유지되는지 확인
5. Client 재연결
6. `DAY15 SharedGoalSnapshot` 확인

### 기대 결과

- Client가 종료되어도 Host Player 및 서버 유지
- Client 재접속 성공
- 남아 있는 Coin 상태와 공동 목표 상태가 최신 상태로 표시

### 실제 결과

DAY14:

- Client 종료 후 Host 유지: 통과
- 같은 Virtual Player 재접속: 통과

DAY15 공유 상태 복원:

`[최종 실행 후 기입]`

### 개인 점수 지속 정책

실제 제출본에서 다음 중 최종 정책을 명확히 적습니다.

- 비로그인 재접속 시 새 Player 점수로 시작
- 로그인 계정 사용 시 DAY13 DB 점수를 로드

핵심 과제의 공동 목표는 NetworkVariable 최신 상태를 받습니다.

---

## 10. DAY14 요청 수 비교

실제로 기록된 값:

| 조건 | 논리 요청 | 실제 송신 RPC | 성공 | 오류 | 시간 | Timeout |
|---|---:|---:|---:|---:|---:|---|
| 변경 전 | 10 | 10 | 10 | 0 | 14.45 ms | False |
| 변경 전 | 100 | 100 | 100 | 0 | 12.12 ms | False |
| Batch10 | 10 | 1 | 10 | 0 | 27.21 ms | False |
| Batch10 | 100 | 10 | 100 | 0 | 15.94 ms | False |

### 분석

Batch10은 실제 송신 RPC 수를 90% 줄였습니다.

그러나 단일 localhost 측정에서는 처리 시간이 개선되지 않았으므로, 배칭이 더 빠르다고 결론 내리지는 않습니다.

---

# UDP Peer 시험

## 11. 정상 위치 송수신

### 실행

Peer A:

```powershell
dotnet run --project UdpPeer/UdpPeer.csproj -- A 127.0.0.1 9001 127.0.0.1 9002
```

Peer B:

```powershell
dotnet run --project UdpPeer/UdpPeer.csproj -- B 127.0.0.1 9002 127.0.0.1 9001
```

### 절차

1. A에서 `3 5`
2. B에서 `show`
3. B에서 `8 2`
4. A에서 `show`

### 기대 결과

상대 PeerId, 마지막 Sequence, X, Y가 표시됩니다.

### 실제 결과

`[실행 후 기입]`

### 판정

`[기입]`

---

## 12. 오래된 Sequence 시험

### 절차

1. A에서 정상 위치를 2회 이상 전송
2. `testold` 실행
3. B 로그 확인

### 기대 결과

```text
[RECV IGNORE OLD]
```

현재 위치는 과거 좌표로 되돌아가지 않습니다.

### 실제 결과

`[실행 후 기입]`

### 판정

`[기입]`

---

## 13. 중복 Sequence 시험

### 절차

A에서 `testdup` 실행

### 기대 결과

같은 Sequence를 다시 적용하지 않고 `RECV IGNORE OLD` 로그를 출력합니다.

### 실제 결과

`[실행 후 기입]`

### 판정

`[기입]`

---

## 14. 잘못된 UDP 메시지 시험

### 절차

- `testinvalid`
- `testempty`

### 기대 결과

```text
[RECV INVALID]
```

로그가 출력되지만 프로그램은 종료되지 않고 이후 정상 메시지를 계속 처리합니다.

### 실제 결과

`[실행 후 기입]`

### 판정

`[기입]`

---

## 15. UDP 10회 전송량 시험

### 절차

1. 양쪽에서 `resetstats`
2. A에서 `load10`
3. A의 `LOAD SEND RESULT` 기록
4. B의 `LOAD RECEIVE RESULT` 기록

### 결과

| 항목 | 값 |
|---|---|
| Requested | 10 |
| Sent | `[기입]` |
| SendErrors | `[기입]` |
| Expected | 10 |
| Received | `[기입]` |
| MissingOrNotObservedYet | `[기입]` |
| SenderElapsedMs | `[기입]` |
| ReceiverWindowMs | `[기입]` |

### 해석

`MissingOrNotObservedYet`는 해당 수신 창에서 관측되지 않은 수이며 실제 패킷 손실로 단정하지 않습니다.

---

## 16. UDP 100회 전송량 시험

### 절차

1. 양쪽에서 `resetstats`
2. A에서 `load100`
3. 송신/수신 결과 기록

### 결과

| 항목 | 값 |
|---|---|
| Requested | 100 |
| Sent | `[기입]` |
| SendErrors | `[기입]` |
| Expected | 100 |
| Received | `[기입]` |
| MissingOrNotObservedYet | `[기입]` |
| SenderElapsedMs | `[기입]` |
| ReceiverWindowMs | `[기입]` |

### 해석

10회 조건과 100회 조건의 송신 수, 수신 관측 수, 오류 수와 시간을 비교합니다.

---

## 17. 최종 회귀 시험표

| 항목 | 결과 |
|---|---|
| Host 시작 | `[최종 확인]` |
| Client 연결 | `[최종 확인]` |
| 각자 Player 조작 | `[최종 확인]` |
| 정상 Coin | `[최종 확인]` |
| 잘못된 ID 거절 | `[최종 확인]` |
| 거리 초과 거절 | `[최종 확인]` |
| 중복 요청 1회 보상 | `[최종 확인]` |
| 3 Coin 게임 성공 | `[최종 확인]` |
| Client 종료 후 Host 유지 | `[최종 확인]` |
| Client 재접속 상태 복원 | `[최종 확인]` |
| UDP 정상 위치 | `[최종 확인]` |
| UDP old/duplicate 무시 | `[최종 확인]` |
| UDP malformed 안전 처리 | `[최종 확인]` |
| UDP load10 | `[최종 확인]` |
| UDP load100 | `[최종 확인]` |

## 18. 수정 및 재시험 기록 양식

### 문제

- 현상:
- 재현 절차:
- Console:
- 예상 원인:

### 수정

- 수정 파일:
- 변경 내용:

### 재시험

- 기대 결과:
- 실제 결과:
- 판정:
