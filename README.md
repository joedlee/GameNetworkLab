ㆍUnity Editor·패키지·.NET SDK 버전

 - Unity 6.6 (6000.6.5f1) <DX12>
 - .NET SDK: .NET 10

ㆍ시작할 씬과 콘솔 프로젝트 위치

ㆍ빌드·실행 방법, Host·Client·UDP 피어를 실행하는 순서



ㆍ접속 주소·포트와 설정을 바꾸는 위치

ㆍ이동·코인 획득·시험 기능의 조작 방법

 - 조작키: WASD
 - 황금색 구체를 총 3개 먹는것

ㆍ정상 실행 여부를 판단할 화면 또는 출력 내용



ㆍ알려진 오류·미완성 기능·환경 제한























## 1. 프로젝트 개요

본 제출물은 두 개의 네트워크 프로그램으로 구성됩니다.

1. Unity NGO 기반 2인 코인 협동 게임
2. .NET UDP Peer 2D 위치 동기화 콘솔 프로그램

Unity 게임에서는 Host와 Client가 각각 플레이어를 생성하고, 각 사용자는 자신의 플레이어만 조작합니다. 코인 획득은 Client가 최종 판정하지 않고 Server에 요청하며, Server가 코인 ID, 거리, 중복 획득 여부를 검사한 뒤 점수와 코인 상태를 동기화합니다.

UDP Peer 프로그램은 두 프로세스가 서로의 2D 위치를 `PeerId / Sequence / X / Y` 형식으로 교환하고, 이전에 적용한 Sequence 이하의 메시지는 무시합니다.

## 2. 개발 환경

### Unity

- Unity: `6000.6.5f1`
- Scene: `Assets/Scenes/NetworkPlay.unity`
- Netcode for GameObjects: `2.13.3`
- Multiplayer Play Mode: `3.0.0`
- Input System: `1.20.0`
- UniTask: `2.5.11`
- Unity Transport: DAY14 시험 기준 `2.6.0`

### UDP Peer

- .NET Target Framework: `net10.0`
- Project: `UdpPeer/UdpPeer.csproj`

## 3. Unity 코인 게임 실행

### 기본 주소

- Host Address: `127.0.0.1`
- Port: `7777`

### 실행 순서

1. Unity에서 `Assets/Scenes/NetworkPlay.unity`를 연다.
2. Multiplayer Play Mode 또는 두 개의 실행 창을 준비한다.
3. 첫 번째 창에서 Host를 시작한다.
4. 두 번째 창에서 Client를 시작한다.
5. 양쪽에 플레이어가 생성되는지 확인한다.
6. 각 창에서 자신의 플레이어만 이동되는지 확인한다.

### 이동

플레이어 이동은 Input System의 `Player/Move` 액션을 사용하며, 실제 이동은 `CharacterController.Move()`로 처리됩니다.

### 코인 게임

- Scene에 코인 3개가 배치되어 있습니다.
- 플레이어가 코인에 접근하면 Client가 Coin NetworkObjectId를 Server로 보냅니다.
- Server가 유효한 코인인지, 허용 거리 안인지, 이미 획득된 코인이 아닌지 검사합니다.
- 승인되면 개인 점수가 1 증가하고 코인이 네트워크에서 제거됩니다.
- 공동 획득 코인이 3개가 되면 양쪽에 `게임 성공`이 표시됩니다.

### 코인 검증 시험 UI

플레이어의 DAY14/DAY15 시험 패널에서 다음 시험을 사용할 수 있습니다.

- 가까운 코인 정상 요청
- 잘못된 Coin ID 요청
- 너무 먼 Coin 요청
- 가까운 코인에 2회 동시 요청

정상 요청은 점수 +1과 코인 제거가 발생해야 합니다. 잘못된 요청은 점수가 변하지 않아야 하며, 중복 요청은 보상이 한 번만 적용되어야 합니다.

### 연결 종료 및 재접속

- Client는 `Disconnect Client` 버튼으로 종료할 수 있습니다.
- Client가 종료되어도 Host는 계속 실행됩니다.
- Client는 다시 연결할 수 있습니다.
- 공유 코인 수와 게임 성공 상태는 NetworkVariable을 통해 최신 상태를 받습니다.

## 4. UDP Peer 실행

### 권장 실행 형식

```powershell
dotnet run --project UdpPeer/UdpPeer.csproj -- <PeerId> <LocalAddress> <LocalPort> <RemoteAddress> <RemotePort>
```

같은 PC에서 두 Peer를 실행하는 예시:

Peer A:

```powershell
dotnet run --project UdpPeer/UdpPeer.csproj -- A 127.0.0.1 9001 127.0.0.1 9002
```

Peer B:

```powershell
dotnet run --project UdpPeer/UdpPeer.csproj -- B 127.0.0.1 9002 127.0.0.1 9001
```

### 명령

- `w/a/s/d`: 자신의 2D 위치 이동 후 전송
- `X Y`: 좌표 직접 지정
- `show`: 자신의 위치와 마지막 원격 상태 확인
- `testold`: 이전 Sequence 메시지 재전송
- `testdup`: 마지막 Sequence 메시지 중복 전송
- `testinvalid`: 잘못된 메시지 전송
- `testempty`: 빈 UDP 메시지 전송
- `load10`: 10개 위치 메시지 전송 시험
- `load100`: 100개 위치 메시지 전송 시험
- `stats`: 통계 확인
- `resetstats`: 통계 초기화

## 5. 예상 결과

### Unity NGO

- Host와 Client가 정상 연결된다.
- 양쪽에서 Player NetworkObject가 생성된다.
- 각 사용자는 자신의 플레이어만 조작한다.
- 코인 획득은 Server가 최종 판정한다.
- 점수와 코인 제거 상태가 두 창에 동일하게 반영된다.
- 코인 3개 획득 후 양쪽에 `게임 성공`이 표시된다.
- Client 종료 후 Host가 유지된다.
- Client 재접속 후 현재 공유 상태가 복원된다.

### UDP

- 두 Peer의 위치가 서로 표시된다.
- Sequence가 증가한다.
- 마지막 적용 Sequence 이하의 메시지는 무시된다.
- 잘못된 메시지를 받아도 프로그램이 종료되지 않는다.
- 10회와 100회 시험 결과가 송신/수신 측에 기록된다.

## 6. 알려진 제한 및 주의

- UDP는 신뢰성 전송을 구현하지 않습니다.
- Sequence 번호가 비었다고 해서 실제 패킷 손실이라고 단정하지 않습니다.
- `MissingOrNotObservedYet`는 해당 측정 창에서 관측되지 않은 메시지 수입니다.
- DAY13의 `Server/GameDataServer`는 추가 기능이며 핵심 제출 프로그램 실행에는 필수가 아닙니다.
- 최종 제출 전 실제 환경에서 Host/Client 및 UDP A/B 실행 결과를 다시 확인합니다.
