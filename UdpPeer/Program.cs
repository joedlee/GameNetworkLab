using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

if (!TryReadConfiguration(
        args,
        out PeerConfiguration configuration))
{
    PrintUsage();
    return;
}

using UdpClient udp =
    new(new IPEndPoint(
        configuration.LocalAddress,
        configuration.LocalPort));

IPEndPoint remoteEndPoint =
    new(
        configuration.RemoteAddress,
        configuration.RemotePort);

using CancellationTokenSource cts =
    new();

int x = 0;
int y = 0;
int sequence = 0;

string? lastSentMessage = null;
string? previousSentMessage = null;

Dictionary<string, RemotePeerState> remoteStates =
    new(StringComparer.Ordinal);

object remoteStatesLock =
    new();

Dictionary<string, LoadReceiveState> loadReceiveStates =
    new(StringComparer.Ordinal);

object loadStatesLock =
    new();

UdpStatistics statistics =
    new();

Console.WriteLine("=== GameNetworkLab UDP Peer ===");
Console.WriteLine(
    $"PeerId={configuration.PeerId} | " +
    $"Local={configuration.LocalAddress}:{configuration.LocalPort} | " +
    $"Remote={configuration.RemoteAddress}:{configuration.RemotePort}");
Console.WriteLine(
    "위치 메시지 형식: POS|PeerId|Sequence|X|Y");
Console.WriteLine();

PrintHelp();

Task receiveTask =
    ReceiveLoopAsync(
        udp,
        configuration.PeerId,
        remoteStates,
        remoteStatesLock,
        loadReceiveStates,
        loadStatesLock,
        statistics,
        cts.Token);

while (!cts.IsCancellationRequested)
{
    Console.Write("> ");

    string? input =
        Console.ReadLine();

    if (input == null)
        break;

    string command =
        input.Trim();

    if (command.Length == 0)
        continue;

    if (command.Equals(
        "quit",
        StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (command.Equals(
        "help",
        StringComparison.OrdinalIgnoreCase))
    {
        PrintHelp();
        continue;
    }

    if (command.Equals(
        "show",
        StringComparison.OrdinalIgnoreCase))
    {
        PrintState(
            configuration.PeerId,
            x,
            y,
            sequence,
            remoteStates,
            remoteStatesLock);

        continue;
    }

    if (command.Equals(
        "stats",
        StringComparison.OrdinalIgnoreCase))
    {
        PrintStatistics(
            statistics,
            remoteStates,
            remoteStatesLock);

        continue;
    }

    if (command.Equals(
        "resetstats",
        StringComparison.OrdinalIgnoreCase))
    {
        ResetStatistics(
            statistics,
            remoteStates,
            remoteStatesLock);

        Console.WriteLine(
            "[STATS] 통계를 초기화했습니다. 마지막 Sequence/위치는 유지됩니다.");

        continue;
    }

    if (command.Equals(
        "load10",
        StringComparison.OrdinalIgnoreCase) ||
        command.Equals(
            "load100",
            StringComparison.OrdinalIgnoreCase))
    {
        int requestCount =
            command.Equals(
                "load10",
                StringComparison.OrdinalIgnoreCase)
                ? 10
                : 100;

        LoadTestResult loadResult =
            await RunLoadTestAsync(
                udp,
                remoteEndPoint,
                configuration.PeerId,
                requestCount,
                x,
                y,
                sequence,
                statistics);

        sequence =
            loadResult.FinalSequence;

        previousSentMessage =
            loadResult.PreviousSentMessage;

        lastSentMessage =
            loadResult.LastSentMessage;

        Console.WriteLine(
            $"[LOAD SEND RESULT] TestId={loadResult.TestId} " +
            $"Requested={requestCount} " +
            $"Sent={loadResult.SentCount} " +
            $"SendErrors={loadResult.SendErrorCount} " +
            $"ControlErrors={loadResult.ControlErrorCount} " +
            $"ElapsedMs={loadResult.ElapsedMilliseconds:F2} " +
            $"SeqRange={loadResult.FirstSequence}-{loadResult.FinalSequence}");

        continue;
    }

    if (command.Equals(
        "testinvalid",
        StringComparison.OrdinalIgnoreCase))
    {
        await SendTestMessageAsync(
            udp,
            remoteEndPoint,
            "BROKEN|MESSAGE",
            "INVALID");

        continue;
    }

    if (command.Equals(
        "testempty",
        StringComparison.OrdinalIgnoreCase))
    {
        await SendTestMessageAsync(
            udp,
            remoteEndPoint,
            string.Empty,
            "EMPTY");

        continue;
    }

    if (command.Equals(
        "testdup",
        StringComparison.OrdinalIgnoreCase))
    {
        if (lastSentMessage == null)
        {
            Console.WriteLine(
                "[TEST] 먼저 정상 위치 메시지를 1회 이상 보내세요.");

            continue;
        }

        await SendTestMessageAsync(
            udp,
            remoteEndPoint,
            lastSentMessage,
            "DUPLICATE");

        continue;
    }

    if (command.Equals(
        "testold",
        StringComparison.OrdinalIgnoreCase))
    {
        if (previousSentMessage == null)
        {
            Console.WriteLine(
                "[TEST] 오래된 메시지 시험은 정상 위치 메시지를 2회 이상 보낸 뒤 실행하세요.");

            continue;
        }

        await SendTestMessageAsync(
            udp,
            remoteEndPoint,
            previousSentMessage,
            "OLD");

        continue;
    }

    bool changed =
        TryApplyMovementCommand(
            command,
            ref x,
            ref y);

    if (!changed)
    {
        changed =
            TryApplyCoordinateCommand(
                command,
                ref x,
                ref y);
    }

    if (!changed)
    {
        Console.WriteLine(
            "알 수 없는 명령입니다. help를 입력해 조작법을 확인하세요.");

        continue;
    }

    sequence++;

    string message =
        PositionMessage.Create(
            configuration.PeerId,
            sequence,
            x,
            y);

    byte[] payload =
        Encoding.UTF8.GetBytes(
            message);

    try
    {
        await udp.SendAsync(
            payload,
            remoteEndPoint);

        statistics.IncrementSent();

        previousSentMessage =
            lastSentMessage;

        lastSentMessage =
            message;

        Console.WriteLine(
            $"[SEND] Peer={configuration.PeerId} Seq={sequence} " +
            $"Pos=({x}, {y}) Bytes={payload.Length}");
    }
    catch (SocketException ex)
    {
        statistics.IncrementSendError();

        Console.WriteLine(
            $"[SEND ERROR] {ex.SocketErrorCode}: {ex.Message}");
    }
}

cts.Cancel();

try
{
    await receiveTask;
}
catch (OperationCanceledException)
{
}

Console.WriteLine("UDP Peer를 종료했습니다.");

static bool TryReadConfiguration(
    string[] args,
    out PeerConfiguration configuration)
{
    configuration = default;

    // 권장 형식:
    // PeerId LocalAddress LocalPort RemoteAddress RemotePort
    if (args.Length >= 5)
    {
        string peerId =
            args[0].Trim();

        if (!TryParseAddress(
                args[1],
                out IPAddress? localAddress) ||
            !int.TryParse(
                args[2],
                out int localPort) ||
            !TryParseAddress(
                args[3],
                out IPAddress? remoteAddress) ||
            !int.TryParse(
                args[4],
                out int remotePort))
        {
            Console.WriteLine(
                "주소 또는 포트 형식이 올바르지 않습니다.");

            return false;
        }

        return TryCreateConfiguration(
            peerId,
            localAddress!,
            localPort,
            remoteAddress!,
            remotePort,
            out configuration);
    }

    // 이전 3인자 형식도 유지:
    // PeerId LocalPort RemotePort -> 127.0.0.1
    if (args.Length == 3 &&
        int.TryParse(
            args[1],
            out int legacyLocalPort) &&
        int.TryParse(
            args[2],
            out int legacyRemotePort))
    {
        Console.WriteLine(
            "[CONFIG] 3인자 호환 모드: Local/Remote 주소를 127.0.0.1로 사용합니다.");

        return TryCreateConfiguration(
            args[0].Trim(),
            IPAddress.Loopback,
            legacyLocalPort,
            IPAddress.Loopback,
            legacyRemotePort,
            out configuration);
    }

    return false;
}

static bool TryParseAddress(
    string raw,
    out IPAddress? address)
{
    if (raw.Equals(
        "localhost",
        StringComparison.OrdinalIgnoreCase))
    {
        address =
            IPAddress.Loopback;

        return true;
    }

    return IPAddress.TryParse(
        raw,
        out address);
}

static bool TryCreateConfiguration(
    string peerId,
    IPAddress localAddress,
    int localPort,
    IPAddress remoteAddress,
    int remotePort,
    out PeerConfiguration configuration)
{
    configuration = default;

    if (string.IsNullOrWhiteSpace(peerId) ||
        peerId.Contains('|'))
    {
        Console.WriteLine(
            "PeerId는 비어 있을 수 없고 '|' 문자를 포함할 수 없습니다.");

        return false;
    }

    if (localPort is < 1 or > 65535 ||
        remotePort is < 1 or > 65535)
    {
        Console.WriteLine(
            "포트 범위는 1~65535입니다.");

        return false;
    }

    if (localAddress.Equals(remoteAddress) &&
        localPort == remotePort)
    {
        Console.WriteLine(
            "Local endpoint와 Remote endpoint는 서로 달라야 합니다.");

        return false;
    }

    configuration =
        new PeerConfiguration(
            peerId,
            localAddress,
            localPort,
            remoteAddress,
            remotePort);

    return true;
}

static async Task<LoadTestResult> RunLoadTestAsync(
    UdpClient udp,
    IPEndPoint remoteEndPoint,
    string peerId,
    int requestCount,
    int x,
    int y,
    int startingSequence,
    UdpStatistics statistics)
{
    int firstSequence =
        startingSequence + 1;

    int finalSequence =
        startingSequence + requestCount;

    string testId =
        $"{peerId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{requestCount}";

    int controlErrors = 0;

    string begin =
        LoadControlMessage.CreateBegin(
            peerId,
            testId,
            requestCount,
            firstSequence,
            finalSequence);

    if (!await TrySendControlAsync(
            udp,
            remoteEndPoint,
            begin,
            "LOAD BEGIN"))
    {
        controlErrors++;
    }

    int sequence =
        startingSequence;

    int sentCount = 0;
    int sendErrorCount = 0;

    string? lastSentMessage = null;
    string? previousSentMessage = null;

    Stopwatch stopwatch =
        Stopwatch.StartNew();

    for (int i = 0;
         i < requestCount;
         i++)
    {
        sequence++;

        string message =
            PositionMessage.Create(
                peerId,
                sequence,
                x,
                y);

        byte[] payload =
            Encoding.UTF8.GetBytes(
                message);

        try
        {
            await udp.SendAsync(
                payload,
                remoteEndPoint);

            sentCount++;
            statistics.IncrementSent();

            previousSentMessage =
                lastSentMessage;

            lastSentMessage =
                message;
        }
        catch (SocketException ex)
        {
            sendErrorCount++;
            statistics.IncrementSendError();

            Console.WriteLine(
                $"[LOAD SEND ERROR] Seq={sequence} " +
                $"{ex.SocketErrorCode}: {ex.Message}");
        }
    }

    stopwatch.Stop();

    string end =
        LoadControlMessage.CreateEnd(
            peerId,
            testId,
            requestCount,
            firstSequence,
            finalSequence,
            stopwatch.Elapsed.TotalMilliseconds);

    if (!await TrySendControlAsync(
            udp,
            remoteEndPoint,
            end,
            "LOAD END"))
    {
        controlErrors++;
    }

    return
        new LoadTestResult(
            testId,
            firstSequence,
            sequence,
            sentCount,
            sendErrorCount,
            controlErrors,
            stopwatch.Elapsed.TotalMilliseconds,
            lastSentMessage,
            previousSentMessage);
}

static async Task<bool> TrySendControlAsync(
    UdpClient udp,
    IPEndPoint remoteEndPoint,
    string rawMessage,
    string label)
{
    try
    {
        byte[] payload =
            Encoding.UTF8.GetBytes(
                rawMessage);

        await udp.SendAsync(
            payload,
            remoteEndPoint);

        return true;
    }
    catch (SocketException ex)
    {
        Console.WriteLine(
            $"[{label} ERROR] {ex.SocketErrorCode}: {ex.Message}");

        return false;
    }
}

static async Task SendTestMessageAsync(
    UdpClient udp,
    IPEndPoint remoteEndPoint,
    string rawMessage,
    string testKind)
{
    byte[] payload =
        Encoding.UTF8.GetBytes(
            rawMessage);

    try
    {
        await udp.SendAsync(
            payload,
            remoteEndPoint);

        Console.WriteLine(
            $"[TEST SEND {testKind}] " +
            $"{(rawMessage.Length == 0 ? "<EMPTY>" : rawMessage)} " +
            $"Bytes={payload.Length}");
    }
    catch (SocketException ex)
    {
        Console.WriteLine(
            $"[TEST SEND ERROR] {ex.SocketErrorCode}: {ex.Message}");
    }
}

static async Task ReceiveLoopAsync(
    UdpClient udp,
    string localPeerId,
    Dictionary<string, RemotePeerState> remoteStates,
    object remoteStatesLock,
    Dictionary<string, LoadReceiveState> loadReceiveStates,
    object loadStatesLock,
    UdpStatistics statistics,
    CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        UdpReceiveResult result;

        try
        {
            result =
                await udp.ReceiveAsync(
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (SocketException ex)
        {
            Console.WriteLine(
                $"[RECV ERROR] {ex.SocketErrorCode}: {ex.Message}");

            continue;
        }

        statistics.IncrementReceived();

        string raw =
            Encoding.UTF8.GetString(
                result.Buffer);

        if (LoadControlMessage.TryParse(
                raw,
                out LoadControlMessage control))
        {
            statistics.IncrementControlReceived();

            HandleLoadControl(
                control,
                loadReceiveStates,
                loadStatesLock);

            continue;
        }

        if (!PositionMessage.TryParse(
                raw,
                out PositionMessage message))
        {
            string displayRaw =
                raw.Length == 0
                    ? "<EMPTY>"
                    : raw;

            statistics.IncrementInvalid();

            Console.WriteLine(
                $"[RECV INVALID] {displayRaw} | Bytes={result.Buffer.Length}");

            continue;
        }

        if (message.PeerId.Equals(
            localPeerId,
            StringComparison.Ordinal))
        {
            statistics.IncrementSelfIgnored();

            Console.WriteLine(
                $"[RECV IGNORE] 자신의 PeerId 메시지: {message.PeerId}");

            continue;
        }

        RegisterLoadPositionIfNeeded(
            message,
            loadReceiveStates,
            loadStatesLock);

        bool ignoredAsOldOrDuplicate = false;
        int lastAppliedSequence = -1;
        int sequenceGap = 0;

        lock (remoteStatesLock)
        {
            if (!remoteStates.TryGetValue(
                    message.PeerId,
                    out RemotePeerState? state))
            {
                state =
                    new RemotePeerState
                    {
                        PeerId =
                            message.PeerId
                    };

                remoteStates.Add(
                    message.PeerId,
                    state);
            }

            if (state.Received &&
                message.Sequence <= state.Sequence)
            {
                ignoredAsOldOrDuplicate = true;
                lastAppliedSequence =
                    state.Sequence;

                state.IgnoredOldOrDuplicateCount++;
            }
            else
            {
                if (state.Received &&
                    message.Sequence > state.Sequence + 1)
                {
                    sequenceGap =
                        message.Sequence - state.Sequence - 1;

                    state.SequenceGapCount +=
                        sequenceGap;
                }

                state.Received = true;
                state.Sequence =
                    message.Sequence;
                state.X =
                    message.X;
                state.Y =
                    message.Y;
                state.AppliedCount++;
                state.LastReceiveUtc =
                    DateTimeOffset.UtcNow;
            }
        }

        if (ignoredAsOldOrDuplicate)
        {
            statistics.IncrementOldOrDuplicateIgnored();

            Console.WriteLine(
                $"\n[RECV IGNORE OLD] Peer={message.PeerId} " +
                $"IncomingSeq={message.Sequence} " +
                $"LastAppliedSeq={lastAppliedSequence} " +
                $"Pos=({message.X}, {message.Y})");

            continue;
        }

        statistics.IncrementApplied();

        string gapText =
            sequenceGap > 0
                ? $" GapObserved={sequenceGap} (손실 확정 아님)"
                : string.Empty;

        Console.WriteLine(
            $"\n[RECV APPLY] Peer={message.PeerId} Seq={message.Sequence} " +
            $"Pos=({message.X}, {message.Y}) From={result.RemoteEndPoint}" +
            gapText);
    }
}

static void HandleLoadControl(
    LoadControlMessage control,
    Dictionary<string, LoadReceiveState> loadReceiveStates,
    object loadStatesLock)
{
    string key =
        control.PeerId + "|" + control.TestId;

    if (control.Kind ==
        LoadControlKind.Begin)
    {
        lock (loadStatesLock)
        {
            loadReceiveStates[key] =
                new LoadReceiveState(
                    control.PeerId,
                    control.TestId,
                    control.ExpectedCount,
                    control.FirstSequence,
                    control.LastSequence);
        }

        Console.WriteLine(
            $"\n[LOAD RECEIVE BEGIN] TestId={control.TestId} " +
            $"Peer={control.PeerId} Expected={control.ExpectedCount} " +
            $"SeqRange={control.FirstSequence}-{control.LastSequence}");

        return;
    }

    LoadReceiveState? state;

    lock (loadStatesLock)
    {
        loadReceiveStates.TryGetValue(
            key,
            out state);

        if (state != null)
        {
            state.SenderElapsedMilliseconds =
                control.SenderElapsedMilliseconds;
        }
    }

    if (state == null)
    {
        Console.WriteLine(
            $"\n[LOAD RECEIVE END WITHOUT BEGIN] TestId={control.TestId} " +
            $"Peer={control.PeerId}");

        return;
    }

    _ =
        FinalizeLoadReceiveAsync(
            key,
            loadReceiveStates,
            loadStatesLock);
}

static void RegisterLoadPositionIfNeeded(
    PositionMessage message,
    Dictionary<string, LoadReceiveState> loadReceiveStates,
    object loadStatesLock)
{
    lock (loadStatesLock)
    {
        foreach (LoadReceiveState state in
                 loadReceiveStates.Values)
        {
            if (!state.PeerId.Equals(
                    message.PeerId,
                    StringComparison.Ordinal) ||
                message.Sequence < state.FirstSequence ||
                message.Sequence > state.LastSequence)
            {
                continue;
            }

            state.ReceivedSequences.Add(
                message.Sequence);
        }
    }
}

static async Task FinalizeLoadReceiveAsync(
    string key,
    Dictionary<string, LoadReceiveState> loadReceiveStates,
    object loadStatesLock)
{
    // UDP는 순서를 보장하지 않으므로 LOADEND보다 늦게 도착하는 위치 패킷을
    // 잠깐 더 받을 시간을 줍니다. 그래도 미수신 수를 '손실'로 단정하지 않습니다.
    await Task.Delay(300);

    LoadReceiveState? state;

    lock (loadStatesLock)
    {
        if (!loadReceiveStates.Remove(
                key,
                out state))
        {
            return;
        }
    }

    int received =
        state.ReceivedSequences.Count;

    int missingOrNotObservedYet =
        Math.Max(
            0,
            state.ExpectedCount - received);

    double receiverWindowMs =
        (DateTimeOffset.UtcNow -
         state.StartedAtUtc)
        .TotalMilliseconds;

    Console.WriteLine(
        $"\n[LOAD RECEIVE RESULT] TestId={state.TestId} " +
        $"Peer={state.PeerId} " +
        $"Expected={state.ExpectedCount} " +
        $"Received={received} " +
        $"MissingOrNotObservedYet={missingOrNotObservedYet} " +
        $"SenderElapsedMs={state.SenderElapsedMilliseconds:F2} " +
        $"ReceiverWindowMs={receiverWindowMs:F2} " +
        $"SeqRange={state.FirstSequence}-{state.LastSequence}");

    Console.WriteLine(
        "[LOAD NOTE] MissingOrNotObservedYet는 관측 결과이며 " +
        "UDP 패킷 손실로 단정하지 않습니다.");
}

static bool TryApplyMovementCommand(
    string command,
    ref int x,
    ref int y)
{
    switch (command.ToLowerInvariant())
    {
        case "w":
            y++;
            return true;

        case "s":
            y--;
            return true;

        case "a":
            x--;
            return true;

        case "d":
            x++;
            return true;

        default:
            return false;
    }
}

static bool TryApplyCoordinateCommand(
    string command,
    ref int x,
    ref int y)
{
    string[] parts =
        command.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length != 2)
        return false;

    if (!int.TryParse(
            parts[0],
            out int newX) ||
        !int.TryParse(
            parts[1],
            out int newY))
    {
        return false;
    }

    x = newX;
    y = newY;

    return true;
}

static void PrintStatistics(
    UdpStatistics statistics,
    Dictionary<string, RemotePeerState> remoteStates,
    object remoteStatesLock)
{
    Console.WriteLine(
        $"[STATS] Sent={statistics.Sent} " +
        $"SendErrors={statistics.SendErrors} " +
        $"ReceivedDatagrams={statistics.ReceivedDatagrams} " +
        $"ControlReceived={statistics.ControlReceived} " +
        $"Applied={statistics.Applied} " +
        $"IgnoredOldOrDuplicate={statistics.IgnoredOldOrDuplicate} " +
        $"Invalid={statistics.Invalid} " +
        $"SelfIgnored={statistics.SelfIgnored}");

    lock (remoteStatesLock)
    {
        if (remoteStates.Count == 0)
        {
            Console.WriteLine(
                "[STATS PEER] 수신된 원격 Peer 없음");

            return;
        }

        foreach (RemotePeerState state in
                 remoteStates.Values.OrderBy(
                     state => state.PeerId,
                     StringComparer.Ordinal))
        {
            string lastReceive =
                state.LastReceiveUtc.HasValue
                    ? state.LastReceiveUtc.Value.ToString("O")
                    : "미수신";

            Console.WriteLine(
                $"[STATS PEER] Peer={state.PeerId} " +
                $"LastSeq={state.Sequence} " +
                $"Pos=({state.X}, {state.Y}) " +
                $"Applied={state.AppliedCount} " +
                $"IgnoredOldOrDuplicate={state.IgnoredOldOrDuplicateCount} " +
                $"SequenceGapObserved={state.SequenceGapCount} " +
                $"LastReceive={lastReceive}");
        }
    }

    Console.WriteLine(
        "[STATS NOTE] SequenceGapObserved는 아직 관측되지 않은 번호이며 " +
        "실제 패킷 손실로 단정하지 않습니다.");
}

static void ResetStatistics(
    UdpStatistics statistics,
    Dictionary<string, RemotePeerState> remoteStates,
    object remoteStatesLock)
{
    statistics.Reset();

    lock (remoteStatesLock)
    {
        foreach (RemotePeerState state in
                 remoteStates.Values)
        {
            state.AppliedCount = 0;
            state.IgnoredOldOrDuplicateCount = 0;
            state.SequenceGapCount = 0;
        }
    }
}

static void PrintState(
    string peerId,
    int x,
    int y,
    int sequence,
    Dictionary<string, RemotePeerState> remoteStates,
    object remoteStatesLock)
{
    Console.WriteLine(
        $"[LOCAL] Peer={peerId} Seq={sequence} Pos=({x}, {y})");

    lock (remoteStatesLock)
    {
        if (remoteStates.Count == 0)
        {
            Console.WriteLine(
                "[REMOTE] 미수신");

            return;
        }

        foreach (RemotePeerState state in
                 remoteStates.Values.OrderBy(
                     state => state.PeerId,
                     StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"[REMOTE] Peer={state.PeerId} " +
                $"Seq={state.Sequence} " +
                $"Pos=({state.X}, {state.Y}) " +
                $"Applied={state.AppliedCount} " +
                $"IgnoredOldOrDuplicate={state.IgnoredOldOrDuplicateCount} " +
                $"SequenceGapObserved={state.SequenceGapCount}");
        }
    }
}

static void PrintHelp()
{
    Console.WriteLine("명령:");
    Console.WriteLine("  w/a/s/d     : 자신의 위치를 한 칸 이동하고 즉시 전송");
    Console.WriteLine("  X Y         : 정수 좌표를 직접 지정하고 즉시 전송 (예: 3 5)");
    Console.WriteLine("  show        : 자신의 위치와 마지막 원격 위치 표시");
    Console.WriteLine("  load10      : 위치 메시지 10개 전송 + 송/수신 결과 측정");
    Console.WriteLine("  load100     : 위치 메시지 100개 전송 + 송/수신 결과 측정");
    Console.WriteLine("  stats       : 송수신/적용/무시/오류/Sequence gap 통계 출력");
    Console.WriteLine("  resetstats  : 통계만 0으로 초기화 (Sequence/위치는 유지)");
    Console.WriteLine("  testinvalid : 필드 수가 틀린 잘못된 메시지를 전송");
    Console.WriteLine("  testempty   : 빈 UDP 메시지를 전송");
    Console.WriteLine("  testdup     : 마지막 메시지를 같은 Sequence/좌표로 재전송");
    Console.WriteLine("  testold     : 그 이전 메시지를 당시 Sequence/좌표 그대로 재전송");
    Console.WriteLine("  help        : 도움말");
    Console.WriteLine("  quit        : 종료");
    Console.WriteLine();
}

static void PrintUsage()
{
    Console.WriteLine(
        "권장 사용법:");
    Console.WriteLine(
        "  dotnet run --project UdpPeer/UdpPeer.csproj -- " +
        "<PeerId> <LocalAddress> <LocalPort> <RemoteAddress> <RemotePort>");

    Console.WriteLine(
        "예시 A:");
    Console.WriteLine(
        "  dotnet run --project UdpPeer/UdpPeer.csproj -- " +
        "A 127.0.0.1 9001 127.0.0.1 9002");

    Console.WriteLine(
        "예시 B:");
    Console.WriteLine(
        "  dotnet run --project UdpPeer/UdpPeer.csproj -- " +
        "B 127.0.0.1 9002 127.0.0.1 9001");

    Console.WriteLine(
        "호환 형식: <PeerId> <LocalPort> <RemotePort>");
}

readonly record struct PeerConfiguration(
    string PeerId,
    IPAddress LocalAddress,
    int LocalPort,
    IPAddress RemoteAddress,
    int RemotePort);

readonly record struct LoadTestResult(
    string TestId,
    int FirstSequence,
    int FinalSequence,
    int SentCount,
    int SendErrorCount,
    int ControlErrorCount,
    double ElapsedMilliseconds,
    string? LastSentMessage,
    string? PreviousSentMessage);

enum LoadControlKind
{
    Begin,
    End
}

readonly record struct LoadControlMessage(
    LoadControlKind Kind,
    string PeerId,
    string TestId,
    int ExpectedCount,
    int FirstSequence,
    int LastSequence,
    double SenderElapsedMilliseconds)
{
    public static string CreateBegin(
        string peerId,
        string testId,
        int expectedCount,
        int firstSequence,
        int lastSequence)
    {
        return
            $"LOADBEGIN|{peerId}|{testId}|{expectedCount}|" +
            $"{firstSequence}|{lastSequence}";
    }

    public static string CreateEnd(
        string peerId,
        string testId,
        int expectedCount,
        int firstSequence,
        int lastSequence,
        double senderElapsedMilliseconds)
    {
        return
            $"LOADEND|{peerId}|{testId}|{expectedCount}|" +
            $"{firstSequence}|{lastSequence}|" +
            senderElapsedMilliseconds.ToString(
                "F4",
                CultureInfo.InvariantCulture);
    }

    public static bool TryParse(
        string raw,
        out LoadControlMessage message)
    {
        message = default;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string[] fields =
            raw.Split('|');

        LoadControlKind kind;

        if (fields[0] == "LOADBEGIN" &&
            fields.Length == 6)
        {
            kind =
                LoadControlKind.Begin;
        }
        else if (fields[0] == "LOADEND" &&
                 fields.Length == 7)
        {
            kind =
                LoadControlKind.End;
        }
        else
        {
            return false;
        }

        string peerId =
            fields[1].Trim();

        string testId =
            fields[2].Trim();

        if (peerId.Length == 0 ||
            testId.Length == 0 ||
            !int.TryParse(
                fields[3],
                out int expectedCount) ||
            expectedCount <= 0 ||
            !int.TryParse(
                fields[4],
                out int firstSequence) ||
            !int.TryParse(
                fields[5],
                out int lastSequence) ||
            firstSequence < 0 ||
            lastSequence < firstSequence)
        {
            return false;
        }

        double senderElapsed =
            0;

        if (kind == LoadControlKind.End &&
            (!double.TryParse(
                fields[6],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out senderElapsed) ||
             senderElapsed < 0))
        {
            return false;
        }

        message =
            new LoadControlMessage(
                kind,
                peerId,
                testId,
                expectedCount,
                firstSequence,
                lastSequence,
                senderElapsed);

        return true;
    }
}

sealed class LoadReceiveState
{
    public LoadReceiveState(
        string peerId,
        string testId,
        int expectedCount,
        int firstSequence,
        int lastSequence)
    {
        PeerId = peerId;
        TestId = testId;
        ExpectedCount = expectedCount;
        FirstSequence = firstSequence;
        LastSequence = lastSequence;
        StartedAtUtc =
            DateTimeOffset.UtcNow;
    }

    public string PeerId { get; }

    public string TestId { get; }

    public int ExpectedCount { get; }

    public int FirstSequence { get; }

    public int LastSequence { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public double SenderElapsedMilliseconds { get; set; }

    public HashSet<int> ReceivedSequences { get; } =
        new();
}

sealed class UdpStatistics
{
    private long sent;
    private long sendErrors;
    private long receivedDatagrams;
    private long controlReceived;
    private long applied;
    private long ignoredOldOrDuplicate;
    private long invalid;
    private long selfIgnored;

    public long Sent =>
        Interlocked.Read(ref sent);

    public long SendErrors =>
        Interlocked.Read(ref sendErrors);

    public long ReceivedDatagrams =>
        Interlocked.Read(ref receivedDatagrams);

    public long ControlReceived =>
        Interlocked.Read(ref controlReceived);

    public long Applied =>
        Interlocked.Read(ref applied);

    public long IgnoredOldOrDuplicate =>
        Interlocked.Read(ref ignoredOldOrDuplicate);

    public long Invalid =>
        Interlocked.Read(ref invalid);

    public long SelfIgnored =>
        Interlocked.Read(ref selfIgnored);

    public void IncrementSent() =>
        Interlocked.Increment(ref sent);

    public void IncrementSendError() =>
        Interlocked.Increment(ref sendErrors);

    public void IncrementReceived() =>
        Interlocked.Increment(ref receivedDatagrams);

    public void IncrementControlReceived() =>
        Interlocked.Increment(ref controlReceived);

    public void IncrementApplied() =>
        Interlocked.Increment(ref applied);

    public void IncrementOldOrDuplicateIgnored() =>
        Interlocked.Increment(ref ignoredOldOrDuplicate);

    public void IncrementInvalid() =>
        Interlocked.Increment(ref invalid);

    public void IncrementSelfIgnored() =>
        Interlocked.Increment(ref selfIgnored);

    public void Reset()
    {
        Interlocked.Exchange(ref sent, 0);
        Interlocked.Exchange(ref sendErrors, 0);
        Interlocked.Exchange(ref receivedDatagrams, 0);
        Interlocked.Exchange(ref controlReceived, 0);
        Interlocked.Exchange(ref applied, 0);
        Interlocked.Exchange(ref ignoredOldOrDuplicate, 0);
        Interlocked.Exchange(ref invalid, 0);
        Interlocked.Exchange(ref selfIgnored, 0);
    }
}

sealed class RemotePeerState
{
    public bool Received { get; set; }

    public string PeerId { get; set; } =
        string.Empty;

    public int Sequence { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int AppliedCount { get; set; }

    public int IgnoredOldOrDuplicateCount { get; set; }

    public int SequenceGapCount { get; set; }

    public DateTimeOffset? LastReceiveUtc { get; set; }
}

readonly record struct PositionMessage(
    string PeerId,
    int Sequence,
    int X,
    int Y)
{
    private const string MessageType =
        "POS";

    public static string Create(
        string peerId,
        int sequence,
        int x,
        int y)
    {
        return
            $"{MessageType}|{peerId}|{sequence}|{x}|{y}";
    }

    public static bool TryParse(
        string raw,
        out PositionMessage message)
    {
        message = default;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string[] fields =
            raw.Split('|');

        if (fields.Length != 5 ||
            !fields[0].Equals(
                MessageType,
                StringComparison.Ordinal))
        {
            return false;
        }

        string senderPeerId =
            fields[1].Trim();

        if (senderPeerId.Length == 0)
            return false;

        if (!int.TryParse(
                fields[2],
                out int sequence) ||
            sequence < 0)
        {
            return false;
        }

        if (!int.TryParse(
                fields[3],
                out int x))
        {
            return false;
        }

        if (!int.TryParse(
                fields[4],
                out int y))
        {
            return false;
        }

        message =
            new PositionMessage(
                senderPeerId,
                sequence,
                x,
                y);

        return true;
    }
}
