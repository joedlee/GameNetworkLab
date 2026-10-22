using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

if (args.Length < 3)
{
    PrintUsage();
    return;
}

string peerId =
    args[0].Trim();

if (string.IsNullOrWhiteSpace(peerId))
{
    Console.WriteLine("PeerId는 비어 있을 수 없습니다.");
    return;
}

if (!int.TryParse(args[1], out int localPort) ||
    !int.TryParse(args[2], out int remotePort))
{
    Console.WriteLine("포트는 정수여야 합니다.");
    PrintUsage();
    return;
}

if (localPort == remotePort)
{
    Console.WriteLine("같은 컴퓨터에서 시험하므로 LocalPort와 RemotePort는 달라야 합니다.");
    return;
}

using UdpClient udp =
    new(new IPEndPoint(
        IPAddress.Loopback,
        localPort));

IPEndPoint remoteEndPoint =
    new(
        IPAddress.Loopback,
        remotePort);

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

UdpStatistics statistics =
    new();

Console.WriteLine("=== GameNetworkLab UDP Peer ===");
Console.WriteLine(
    $"PeerId={peerId} | Local=127.0.0.1:{localPort} | Remote=127.0.0.1:{remotePort}");
Console.WriteLine(
    "메시지 형식: POS|PeerId|Sequence|X|Y");
Console.WriteLine();

PrintHelp();

Task receiveTask =
    ReceiveLoopAsync(
        udp,
        peerId,
        remoteStates,
        remoteStatesLock,
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
            peerId,
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
                peerId,
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
            $"[LOAD RESULT] Requested={requestCount} " +
            $"Sent={loadResult.SentCount} " +
            $"SendErrors={loadResult.SendErrorCount} " +
            $"ElapsedMs={loadResult.ElapsedMilliseconds:F2} " +
            $"FinalSeq={loadResult.FinalSequence}");

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

        statistics.IncrementSent();

        previousSentMessage =
            lastSentMessage;

        lastSentMessage =
            message;

        Console.WriteLine(
            $"[SEND] Peer={peerId} Seq={sequence} Pos=({x}, {y}) Bytes={payload.Length}");
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

    return
        new LoadTestResult(
            sequence,
            sentCount,
            sendErrorCount,
            stopwatch.Elapsed.TotalMilliseconds,
            lastSentMessage,
            previousSentMessage);
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
            $"[TEST SEND {testKind}] {rawMessage} Bytes={payload.Length}");
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

        string raw =
            Encoding.UTF8.GetString(
                result.Buffer);

        statistics.IncrementReceived();

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

        bool ignoredAsOldOrDuplicate = false;
        int lastAppliedSequence = -1;

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
                state.Received = true;
                state.Sequence =
                    message.Sequence;
                state.X =
                    message.X;
                state.Y =
                    message.Y;
                state.AppliedCount++;
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

        Console.WriteLine(
            $"\n[RECV APPLY] Peer={message.PeerId} Seq={message.Sequence} " +
            $"Pos=({message.X}, {message.Y}) From={result.RemoteEndPoint}");
    }
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
        $"Applied={statistics.Applied} " +
        $"IgnoredOldOrDuplicate={statistics.IgnoredOldOrDuplicate} " +
        $"Invalid={statistics.Invalid} " +
        $"SelfIgnored={statistics.SelfIgnored}");

    lock (remoteStatesLock)
    {
        foreach (RemotePeerState state in
                 remoteStates.Values.OrderBy(
                     state => state.PeerId,
                     StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"[STATS PEER] Peer={state.PeerId} " +
                $"LastSeq={state.Sequence} " +
                $"Pos=({state.X}, {state.Y}) " +
                $"Applied={state.AppliedCount} " +
                $"IgnoredOldOrDuplicate={state.IgnoredOldOrDuplicateCount}");
        }
    }
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
                $"IgnoredOldOrDuplicate={state.IgnoredOldOrDuplicateCount}");
        }
    }
}

static void PrintHelp()
{
    Console.WriteLine("명령:");
    Console.WriteLine("  w/a/s/d  : 자신의 위치를 한 칸 이동하고 즉시 전송");
    Console.WriteLine("  X Y      : 정수 좌표를 직접 지정하고 즉시 전송 (예: 3 5)");
    Console.WriteLine("  show        : 자신의 위치와 마지막으로 받은 상대 위치 표시");
    Console.WriteLine("  load10      : 현재 위치 메시지 10개를 연속 전송하고 시간 측정");
    Console.WriteLine("  load100     : 현재 위치 메시지 100개를 연속 전송하고 시간 측정");
    Console.WriteLine("  stats       : 송수신/적용/무시/오류 통계 출력");
    Console.WriteLine("  resetstats  : 통계만 0으로 초기화 (Sequence/위치는 유지)");
    Console.WriteLine("  testinvalid : 필드 수가 틀린 잘못된 메시지를 전송");
    Console.WriteLine("  testempty   : 빈 UDP 메시지를 전송");
    Console.WriteLine("  testdup     : 마지막으로 보낸 메시지를 같은 Sequence/좌표로 재전송");
    Console.WriteLine("  testold     : 그 이전 메시지를 당시 Sequence/좌표 그대로 재전송");
    Console.WriteLine("  help        : 도움말");
    Console.WriteLine("  quit     : 종료");
    Console.WriteLine();
}

static void PrintUsage()
{
    Console.WriteLine(
        "사용법: dotnet run --project UdpPeer/UdpPeer.csproj -- <PeerId> <LocalPort> <RemotePort>");

    Console.WriteLine(
        "예시 A: dotnet run --project UdpPeer/UdpPeer.csproj -- A 9001 9002");

    Console.WriteLine(
        "예시 B: dotnet run --project UdpPeer/UdpPeer.csproj -- B 9002 9001");
}

readonly record struct LoadTestResult(
    int FinalSequence,
    int SentCount,
    int SendErrorCount,
    double ElapsedMilliseconds,
    string? LastSentMessage,
    string? PreviousSentMessage);

sealed class UdpStatistics
{
    private long sent;
    private long sendErrors;
    private long receivedDatagrams;
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
