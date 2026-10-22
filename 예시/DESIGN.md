# GameNetworkLab 제출 문서 예시 - DESIGN

> 이 파일은 설계 문서 작성 예시입니다. 실제 제출 시 직접 확인한 동작과 최종 정책에 맞춰 수정합니다.

## 1. 요구사항과 역할 분리

프로젝트는 Unity NGO 코인 게임과 UDP Peer 프로그램으로 구성됩니다.

Unity NGO에서는 Client가 플레이어 입력과 상호작용 요청을 발생시키고, Server가 권한 있는 상태 변경을 판정합니다.

핵심 원칙은 다음과 같습니다.

```text
Client 입력/요청
→ Server 검증
→ Server 상태 변경
→ NetworkVariable / NGO 동기화
→ 각 Client UI 갱신
```

Client가 점수나 코인 제거를 직접 확정하지 않도록 구성했습니다.

## 2. 플레이어 이동

각 Player NetworkObject는 자신을 소유한 Client에서만 입력을 읽습니다.

`NetworkPlayerMove`의 입력 흐름:

```text
Owner Client
→ Player/Move 입력
→ Server RPC
→ Server에서 CharacterController.Move()
```

따라서 원격 플레이어의 입력을 로컬 사용자가 직접 조작하지 않습니다.

## 3. 코인 데이터 흐름

코인 획득 흐름:

```text
플레이어가 코인 Trigger 진입
→ Owner Client가 Coin NetworkObjectId 확보
→ RequestCollectCoin()
→ Server RPC
→ SpawnedObjects에서 ID 검색
→ NetworkCoin 여부 검사
→ 획득 여부 검사
→ 플레이어-코인 거리 검사
→ 승인
→ 개인 Score +1
→ 공동 CollectedCoins +1
→ Coin NetworkObject Despawn
```

### Server 검증 규칙

Server는 다음 요청을 거절합니다.

1. 존재하지 않는 NetworkObjectId
2. 이미 제거된 Coin ID
3. NetworkCoin이 아닌 NetworkObject
4. 이미 획득 처리된 Coin
5. 허용 거리보다 먼 플레이어의 요청
6. 동일 Coin에 대한 중복 요청

동시에 같은 Coin을 두 번 요청해도 첫 번째 요청이 처리된 뒤 Coin이 제거되므로 보상은 한 번만 발생하도록 설계했습니다.

## 4. 점수 정책

### 개인 점수

각 `NetworkScorePlayer`는 `NetworkVariable<int> Score`를 가집니다.

코인 획득이 Server에서 승인되었을 때만 `AddScoreFromServer(1)`이 호출됩니다.

검증 없이 점수를 올리던 기존 디버그 `RequestAddScore` 경로는 제거했습니다.

### 공동 목표

씬의 `NetworkCoopGoal`은 다음 공유 상태를 가집니다.

- `CollectedCoins`
- `GameSucceeded`

Server가 코인 획득을 승인할 때마다 `CollectedCoins`가 증가합니다.

```text
CollectedCoins >= 3
AND
GameSucceeded == false
→ GameSucceeded = true
```

이미 성공한 이후에는 다시 성공 처리하지 않습니다.

## 5. 연결과 재접속

기본 NGO 주소는 다음과 같습니다.

- Address: `127.0.0.1`
- Port: `7777`

연결 UI에서는 Host/Client 중복 시작을 막고, Client 연결 실패 또는 시간 초과 시 재시도할 수 있도록 했습니다.

Client가 종료되어도 Host는 계속 동작합니다.

재접속한 Client는 NGO NetworkVariable의 현재값을 받아 공동 코인 수와 성공 상태를 확인합니다. `NetworkCoopGoal.OnNetworkSpawn()`에서 현재 공유 상태를 `SharedGoalSnapshot` 로그로 남겨 복원 확인을 쉽게 했습니다.

개인 점수의 계정 DB 저장 기능은 DAY13의 추가 기능이며, 핵심 과제의 실시간 네트워크 공유 상태와 분리해 설명합니다.

## 6. UDP 메시지 설계

위치 메시지는 다음 형식을 사용합니다.

```text
POS|PeerId|Sequence|X|Y
```

필드 의미:

| 필드 | 의미 |
|---|---|
| PeerId | 송신 Peer 식별자 |
| Sequence | 새 위치 메시지마다 증가하는 순번 |
| X | 2D X 좌표 |
| Y | 2D Y 좌표 |

예:

```text
POS|A|15|3|5
```

## 7. UDP 최신 상태 규칙

각 송신 Peer별 마지막 적용 Sequence를 저장합니다.

수신한 메시지가:

```text
IncomingSequence <= LastAppliedSequence
```

이면 해당 메시지를 적용하지 않습니다.

따라서 늦게 도착한 오래된 위치나 같은 메시지의 중복 수신으로 위치가 과거 상태로 되돌아가지 않습니다.

`testold`와 `testdup` 명령으로 이 규칙을 직접 시험할 수 있습니다.

## 8. 잘못된 UDP 메시지 처리

메시지 형식이 맞지 않거나 필드 파싱에 실패하면 `[RECV INVALID]` 로그만 남기고 수신 루프는 계속 실행합니다.

시험 명령:

- `testinvalid`
- `testempty`

이 시험은 잘못된 Datagram이 프로그램 전체를 종료시키지 않는지 확인하기 위한 것입니다.

## 9. UDP 부하 시험

두 조건을 제공합니다.

- `load10`
- `load100`

시험 시 송신 측은 다음을 기록합니다.

- 요청 메시지 수
- 실제 전송 성공 수
- 전송 오류 수
- 송신 소요 시간
- Sequence 범위

수신 측은 LOAD BEGIN/END 제어 메시지를 이용해 다음을 기록합니다.

- Expected
- Received
- MissingOrNotObservedYet
- SenderElapsedMs
- ReceiverWindowMs

UDP는 신뢰성 프로토콜이 아니므로 `MissingOrNotObservedYet`를 바로 패킷 손실이라고 표현하지 않습니다.

## 10. 실시간 데이터와 영속 데이터 구분

### 실시간

- 플레이어 위치
- 코인 존재 여부
- 개인 현재 점수
- 공동 코인 수
- 게임 성공 여부
- UDP Peer 현재 위치/Sequence

### 영속 데이터

DAY13의 계정 점수 DB는 추가 기능입니다. 로그인 계정 점수 저장은 별도 HTTP/DB 경로를 사용하며 이번 NGO 코인 동기화의 권한 판정 구조와 구분합니다.

## 11. 로그

주요 로그 예:

```text
HostStarted
ClientConnected
ClientDisconnected
Coin 요청 승인
Coin 요청 거절
DAY15 DuplicateCoinTestResult
DAY15 GameSuccess
DAY15 SharedGoalSnapshot
[RECV APPLY]
[RECV IGNORE OLD]
[RECV INVALID]
[LOAD SEND RESULT]
[LOAD RECEIVE RESULT]
```

## 12. 참고 자료와 직접 변경 코드 구분 예시

실제 제출 시 아래 표를 본인 작업 기준으로 수정합니다.

| 구분 | 내용 |
|---|---|
| Unity/공식 API | Netcode for GameObjects, Unity Transport, CharacterController, Input System 사용법 |
| 외부 패키지 | UniTask 2.5.11 |
| 직접 구성/수정 | 코인 Server 검증, 공동 목표, 요청 시험 기능, 접속 UI, UDP Peer 위치/Sequence 처리, 부하 시험 |
| 추가 기능 | DAY13 GameDataServer 및 계정 점수 저장 |

수업에서 AI 사용 표기 방식에 대한 별도 구두 지침이 있었다면 그 지침에 맞춰 실제 제출본에 추가합니다.
