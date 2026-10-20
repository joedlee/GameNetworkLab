using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

public class NetworkRequestBenchmark : NetworkBehaviour
{
    private const double TimeoutSeconds = 5.0;

    private readonly HashSet<int> receivedRequestIds = new();
    private readonly Stopwatch stopwatch = new();

    private int currentRunId;
    private int requestedCount;
    private int successCount;
    private int errorCount;
    private bool running;

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

        const float width = 300f;
        const float height = 190f;

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

        if (GUILayout.Button(
            "10회 요청 테스트",
            GUILayout.Height(32f)))
        {
            StartBenchmark(10);
        }

        if (GUILayout.Button(
            "100회 요청 테스트",
            GUILayout.Height(32f)))
        {
            StartBenchmark(100);
        }

        GUI.enabled = true;

        GUILayout.Space(6f);
        GUILayout.Label(resultText);

        GUILayout.EndArea();
    }

    private void StartBenchmark(
        int count)
    {
        if (!IsOwner ||
            NetworkManager == null ||
            !NetworkManager.IsListening ||
            running)
        {
            return;
        }

        currentRunId++;
        requestedCount = count;
        successCount = 0;
        errorCount = 0;
        receivedRequestIds.Clear();

        resultText =
            $"측정 중... 요청 {count}회";

        running = true;

        stopwatch.Restart();

        for (int requestId = 0;
             requestId < count;
             requestId++)
        {
            BenchmarkRequestRpc(
                currentRunId,
                requestId);
        }

        UnityEngine.Debug.Log(
            $"DAY14 BenchmarkStart | " +
            $"ClientId={OwnerClientId} | " +
            $"Run={currentRunId} | " +
            $"Requests={count}");
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
        if (!IsOwner ||
            !running ||
            runId != currentRunId)
        {
            return;
        }

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

        resultText =
            $"요청: {requestedCount}\n" +
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
            $"ClientId={OwnerClientId} | " +
            $"Requests={requestedCount} | " +
            $"Success={successCount} | " +
            $"Errors={errorCount} | " +
            $"ElapsedMs={elapsedMilliseconds:F2} | " +
            $"Timeout={timeout}");
    }
}
