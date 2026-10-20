using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

public class NetworkRequestBenchmark : NetworkBehaviour
{
    private const double TimeoutSeconds = 5.0;
    private const int TunedBatchSize = 10;

    private readonly HashSet<int> receivedRequestIds = new();
    private readonly Stopwatch stopwatch = new();

    private int currentRunId;
    private int requestedCount;
    private int successCount;
    private int errorCount;
    private int sentRpcCount;
    private bool running;
    private bool tunedMode;

    private string resultText =
        "DAY14 요청 테스트 대기";

    private void Update()
    {
        if (!running)
            return;

        if (stopwatch.Elapsed.TotalSeconds < TimeoutSeconds)
            return;

        FinishBenchmark(timeout: true);
    }

    private void OnGUI()
    {
        if (!IsSpawned || !IsOwner)
            return;

        const float width = 320f;
        const float height = 290f;

        Rect panelRect =
            new Rect(
                20f,
                20f,
                width,
                height);

        GUILayout.BeginArea(
            panelRect,
            GUI.skin.box);

        GUILayout.Label("DAY14 요청 벤치마크");

        GUI.enabled =
            !running &&
            NetworkManager != null &&
            NetworkManager.IsListening;

        GUILayout.Label("변경 전 - 개별 RPC");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
            "10회",
            GUILayout.Height(30f)))
        {
            StartIndividualBenchmark(10);
        }

        if (GUILayout.Button(
            "100회",
            GUILayout.Height(30f)))
        {
            StartIndividualBenchmark(100);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label("튜닝 후 - 10개씩 묶음 전송");

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
            "튜닝 10회",
            GUILayout.Height(30f)))
        {
            StartTunedBenchmark(10);
        }

        if (GUILayout.Button(
            "튜닝 100회",
            GUILayout.Height(30f)))
        {
            StartTunedBenchmark(100);
        }

        GUILayout.EndHorizontal();

        GUI.enabled = true;

        GUILayout.Space(6f);
        GUILayout.Label(resultText);

        GUILayout.EndArea();
    }

    private void StartIndividualBenchmark(
        int count)
    {
        if (!PrepareBenchmark(
            count,
            useTunedMode: false))
        {
            return;
        }

        sentRpcCount = count;

        stopwatch.Restart();

        for (int requestId = 0;
             requestId < count;
             requestId++)
        {
            BenchmarkRequestRpc(
                currentRunId,
                requestId);
        }

        LogStart();
    }

    private void StartTunedBenchmark(
        int count)
    {
        if (!PrepareBenchmark(
            count,
            useTunedMode: true))
        {
            return;
        }

        sentRpcCount =
            Mathf.CeilToInt(
                count / (float)TunedBatchSize);

        stopwatch.Restart();

        for (int batchStart = 0;
             batchStart < count;
             batchStart += TunedBatchSize)
        {
            int batchSize =
                Mathf.Min(
                    TunedBatchSize,
                    count - batchStart);

            BenchmarkBatchRequestRpc(
                currentRunId,
                batchStart,
                batchSize);
        }

        LogStart();
    }

    private bool PrepareBenchmark(
        int count,
        bool useTunedMode)
    {
        if (!IsOwner ||
            NetworkManager == null ||
            !NetworkManager.IsListening ||
            running)
        {
            return false;
        }

        currentRunId++;
        requestedCount = count;
        successCount = 0;
        errorCount = 0;
        sentRpcCount = 0;
        tunedMode = useTunedMode;

        receivedRequestIds.Clear();

        string modeName =
            tunedMode
                ? $"튜닝 후 (Batch {TunedBatchSize})"
                : "변경 전 (개별 RPC)";

        resultText =
            $"{modeName}\n측정 중... 논리 요청 {count}회";

        running = true;

        return true;
    }

    [Rpc(SendTo.Server)]
    private void BenchmarkRequestRpc(
        int runId,
        int requestId)
    {
        BenchmarkResponseRpc(
            runId,
            requestId);
    }

    [Rpc(SendTo.Owner)]
    private void BenchmarkResponseRpc(
        int runId,
        int requestId)
    {
        if (!IsCurrentRun(runId))
            return;

        RecordLogicalSuccess(
            requestId);
    }

    [Rpc(SendTo.Server)]
    private void BenchmarkBatchRequestRpc(
        int runId,
        int batchStart,
        int batchSize)
    {
        BenchmarkBatchResponseRpc(
            runId,
            batchStart,
            batchSize);
    }

    [Rpc(SendTo.Owner)]
    private void BenchmarkBatchResponseRpc(
        int runId,
        int batchStart,
        int batchSize)
    {
        if (!IsCurrentRun(runId))
            return;

        for (int offset = 0;
             offset < batchSize;
             offset++)
        {
            RecordLogicalSuccess(
                batchStart + offset);
        }
    }

    private void RecordLogicalSuccess(
        int requestId)
    {
        if (!receivedRequestIds.Add(requestId))
            return;

        successCount =
            receivedRequestIds.Count;

        if (successCount >= requestedCount)
        {
            FinishBenchmark(
                timeout: false);
        }
    }

    private bool IsCurrentRun(
        int runId)
    {
        return
            IsOwner &&
            running &&
            runId == currentRunId;
    }

    private void FinishBenchmark(
        bool timeout)
    {
        if (!running)
            return;

        stopwatch.Stop();
        running = false;

        successCount =
            receivedRequestIds.Count;

        errorCount =
            Mathf.Max(
                0,
                requestedCount - successCount);

        double elapsedMilliseconds =
            stopwatch.Elapsed.TotalMilliseconds;

        string modeName =
            tunedMode
                ? $"튜닝 후 (Batch {TunedBatchSize})"
                : "변경 전 (개별 RPC)";

        resultText =
            $"{modeName}\n" +
            $"논리 요청: {requestedCount}\n" +
            $"실제 송신 RPC: {sentRpcCount}\n" +
            $"성공: {successCount}\n" +
            $"오류: {errorCount}\n" +
            $"시간: {elapsedMilliseconds:F2} ms";

        if (timeout)
        {
            resultText +=
                "\n5초 제한시간 종료";
        }

        UnityEngine.Debug.Log(
            $"DAY14 BenchmarkResult | " +
            $"Mode={(tunedMode ? "Batch10" : "Individual")} | " +
            $"ClientId={OwnerClientId} | " +
            $"Requests={requestedCount} | " +
            $"SentRpc={sentRpcCount} | " +
            $"Success={successCount} | " +
            $"Errors={errorCount} | " +
            $"ElapsedMs={elapsedMilliseconds:F2} | " +
            $"Timeout={timeout}");
    }

    private void LogStart()
    {
        UnityEngine.Debug.Log(
            $"DAY14 BenchmarkStart | " +
            $"Mode={(tunedMode ? "Batch10" : "Individual")} | " +
            $"ClientId={OwnerClientId} | " +
            $"Run={currentRunId} | " +
            $"Requests={requestedCount} | " +
            $"SentRpc={sentRpcCount}");
    }
}
