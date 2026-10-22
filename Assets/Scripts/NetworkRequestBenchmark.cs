using System.Collections;
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

    private NetworkScorePlayer scorePlayer;

    private string resultText =
        "DAY14 요청 테스트 대기";

    private void Awake()
    {
        scorePlayer =
            GetComponent<NetworkScorePlayer>();
    }

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
        const float height = 500f;

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

        GUILayout.Space(8f);
        GUILayout.Label("DAY15 코인 요청 검증");

        if (GUILayout.Button(
            "가까운 코인 정상 요청",
            GUILayout.Height(30f)))
        {
            TestValidCoinRequest();
        }

        if (GUILayout.Button(
            "잘못된 Coin ID 요청",
            GUILayout.Height(30f)))
        {
            TestInvalidCoinId();
        }

        if (GUILayout.Button(
            "너무 먼 Coin 요청",
            GUILayout.Height(30f)))
        {
            TestTooFarCoinRequest();
        }

        if (GUILayout.Button(
            "가까운 코인에 2회 동시 요청",
            GUILayout.Height(30f)))
        {
            TestDuplicateCoinRequest();
        }

        GUI.enabled = true;

        GUILayout.Space(6f);
        GUILayout.Label(resultText);

        GUILayout.EndArea();
    }

    private void TestValidCoinRequest()
    {
        if (scorePlayer == null)
        {
            resultText =
                "정상 요청 시험 실패: NetworkScorePlayer 없음";

            return;
        }

        NetworkCoin[] coins =
            FindObjectsByType<NetworkCoin>(
                FindObjectsSortMode.None);

        NetworkCoin nearestCoin = null;
        float nearestDistance =
            float.MaxValue;

        foreach (NetworkCoin coin in coins)
        {
            if (coin == null ||
                !coin.IsSpawned)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    coin.transform.position);

            if (distance >= nearestDistance)
                continue;

            nearestCoin =
                coin;

            nearestDistance =
                distance;
        }

        if (nearestCoin == null)
        {
            resultText =
                "정상 요청 시험 실패: 남아 있는 코인 없음";

            return;
        }

        if (nearestDistance > 2f)
        {
            resultText =
                $"가까운 코인까지 거리 {nearestDistance:F2}\n" +
                "2.0 이내로 이동 후 다시 실행하세요.";

            return;
        }

        int scoreBefore =
            scorePlayer.Score.Value;

        ulong coinId =
            nearestCoin.NetworkObjectId;

        UnityEngine.Debug.Log(
            $"DAY15 ValidCoinTestStart | " +
            $"ClientId={OwnerClientId} | " +
            $"CoinNetworkObjectId={coinId} | " +
            $"Distance={nearestDistance:F2} | " +
            $"ScoreBefore={scoreBefore}");

        scorePlayer.RequestCollectCoin(
            coinId);

        resultText =
            $"정상 Coin 요청 전송\n" +
            $"Coin ID: {coinId}\n" +
            $"거리: {nearestDistance:F2}\n" +
            "기대 결과: Server 승인 / 점수 +1 / 코인 제거";

        StartCoroutine(
            CheckValidCoinRequestResult(
                coinId,
                scoreBefore));
    }

    private IEnumerator CheckValidCoinRequestResult(
        ulong coinId,
        int scoreBefore)
    {
        yield return new WaitForSeconds(
            0.5f);

        if (scorePlayer == null)
            yield break;

        int scoreAfter =
            scorePlayer.Score.Value;

        bool coinStillSpawned =
            NetworkManager != null &&
            NetworkManager.SpawnManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(
                coinId);

        bool passed =
            scoreAfter == scoreBefore + 1 &&
            !coinStillSpawned;

        resultText =
            $"정상 요청 시험 {(passed ? "통과" : "확인 필요")}\n" +
            $"점수 변화: {scoreBefore} → {scoreAfter}\n" +
            $"코인 제거: {!coinStillSpawned}";

        UnityEngine.Debug.Log(
            $"DAY15 ValidCoinTestResult | " +
            $"ClientId={OwnerClientId} | " +
            $"CoinNetworkObjectId={coinId} | " +
            $"ScoreBefore={scoreBefore} | " +
            $"ScoreAfter={scoreAfter} | " +
            $"CoinRemoved={!coinStillSpawned} | " +
            $"Pass={passed}");
    }

    private void TestInvalidCoinId()
    {
        if (scorePlayer == null)
        {
            resultText =
                "잘못된 ID 시험 실패: NetworkScorePlayer 없음";

            return;
        }

        const ulong invalidCoinId =
            ulong.MaxValue;

        int scoreBefore =
            scorePlayer.Score.Value;

        UnityEngine.Debug.Log(
            $"DAY15 InvalidCoinIdTestStart | " +
            $"ClientId={OwnerClientId} | " +
            $"CoinNetworkObjectId={invalidCoinId} | " +
            $"ScoreBefore={scoreBefore}");

        scorePlayer.RequestCollectCoin(
            invalidCoinId);

        resultText =
            $"잘못된 Coin ID 요청 전송\n" +
            $"Coin ID: {invalidCoinId}\n" +
            $"기대 결과: Server 거절 / 점수 변화 없음";

        StartCoroutine(
            CheckRejectedRequestResult(
                "InvalidCoinId",
                scoreBefore));
    }

    private void TestTooFarCoinRequest()
    {
        if (scorePlayer == null)
        {
            resultText =
                "거리 초과 시험 실패: NetworkScorePlayer 없음";

            return;
        }

        NetworkCoin[] coins =
            FindObjectsByType<NetworkCoin>(
                FindObjectsSortMode.None);

        NetworkCoin farthestCoin = null;
        float farthestDistance = 0f;

        foreach (NetworkCoin coin in coins)
        {
            if (coin == null ||
                !coin.IsSpawned)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    coin.transform.position);

            if (distance <= farthestDistance)
                continue;

            farthestCoin = coin;
            farthestDistance = distance;
        }

        if (farthestCoin == null)
        {
            resultText =
                "거리 초과 시험 실패: 남아 있는 코인 없음";

            return;
        }

        if (farthestDistance <= 2f)
        {
            resultText =
                $"모든 코인이 너무 가깝습니다.\n" +
                $"코인에서 더 멀리 이동 후 다시 실행하세요.\n" +
                $"현재 최대 거리: {farthestDistance:F2}";

            return;
        }

        int scoreBefore =
            scorePlayer.Score.Value;

        ulong coinId =
            farthestCoin.NetworkObjectId;

        UnityEngine.Debug.Log(
            $"DAY15 TooFarCoinTestStart | " +
            $"ClientId={OwnerClientId} | " +
            $"CoinNetworkObjectId={coinId} | " +
            $"Distance={farthestDistance:F2} | " +
            $"ScoreBefore={scoreBefore}");

        scorePlayer.RequestCollectCoin(
            coinId);

        resultText =
            $"거리 초과 Coin 요청 전송\n" +
            $"Coin ID: {coinId}\n" +
            $"거리: {farthestDistance:F2}\n" +
            $"기대 결과: Server 거절 / 점수 변화 없음";

        StartCoroutine(
            CheckRejectedRequestResult(
                "TooFar",
                scoreBefore));
    }

    private IEnumerator CheckRejectedRequestResult(
        string testName,
        int scoreBefore)
    {
        yield return new WaitForSeconds(
            0.5f);

        if (scorePlayer == null)
            yield break;

        int scoreAfter =
            scorePlayer.Score.Value;

        bool passed =
            scoreAfter == scoreBefore;

        resultText =
            $"{testName} 시험 {(passed ? "통과" : "실패")}\n" +
            $"점수 변화: {scoreBefore} → {scoreAfter}\n" +
            $"기대: 변화 없음";

        UnityEngine.Debug.Log(
            $"DAY15 RejectedCoinRequestTestResult | " +
            $"Test={testName} | " +
            $"ClientId={OwnerClientId} | " +
            $"ScoreBefore={scoreBefore} | " +
            $"ScoreAfter={scoreAfter} | " +
            $"Pass={passed}");
    }

    private void TestDuplicateCoinRequest()
    {
        if (scorePlayer == null)
        {
            resultText =
                "중복 요청 시험 실패: NetworkScorePlayer 없음";

            return;
        }

        NetworkCoin[] coins =
            FindObjectsByType<NetworkCoin>(
                FindObjectsSortMode.None);

        NetworkCoin nearestCoin = null;
        float nearestDistance =
            float.MaxValue;

        foreach (NetworkCoin coin in coins)
        {
            if (coin == null ||
                !coin.IsSpawned)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    coin.transform.position);

            if (distance >= nearestDistance)
                continue;

            nearestCoin = coin;
            nearestDistance = distance;
        }

        if (nearestCoin == null)
        {
            resultText =
                "중복 요청 시험 실패: 남아 있는 코인 없음";

            return;
        }

        if (nearestDistance > 2f)
        {
            resultText =
                $"코인에 더 가까이 이동하세요. 거리 {nearestDistance:F2}";

            return;
        }

        int scoreBefore =
            scorePlayer.Score.Value;

        ulong coinId =
            nearestCoin.NetworkObjectId;

        UnityEngine.Debug.Log(
            $"DAY15 DuplicateCoinTestStart | " +
            $"ClientId={OwnerClientId} | " +
            $"CoinNetworkObjectId={coinId} | " +
            $"ScoreBefore={scoreBefore} | " +
            $"Requests=2");

        scorePlayer.RequestCollectCoin(
            coinId);

        scorePlayer.RequestCollectCoin(
            coinId);

        resultText =
            $"중복 요청 전송 완료\n" +
            $"Coin ID: {coinId}\n" +
            $"요청: 2회 / 기대 보상: 1회";

        StartCoroutine(
            CheckDuplicateCoinResult(
                coinId,
                scoreBefore));
    }

    private IEnumerator CheckDuplicateCoinResult(
        ulong coinId,
        int scoreBefore)
    {
        yield return new WaitForSeconds(
            0.5f);

        if (scorePlayer == null)
            yield break;

        int scoreAfter =
            scorePlayer.Score.Value;

        int scoreDelta =
            scoreAfter - scoreBefore;

        bool passed =
            scoreDelta == 1;

        resultText =
            $"중복 요청 시험 {(passed ? "통과" : "확인 필요")}\n" +
            $"요청: 2회\n" +
            $"점수 변화: {scoreBefore} → {scoreAfter}\n" +
            $"보상 횟수: {scoreDelta}";

        UnityEngine.Debug.Log(
            $"DAY15 DuplicateCoinTestResult | " +
            $"ClientId={OwnerClientId} | " +
            $"CoinNetworkObjectId={coinId} | " +
            $"Requests=2 | " +
            $"ScoreBefore={scoreBefore} | " +
            $"ScoreAfter={scoreAfter} | " +
            $"RewardCount={scoreDelta} | " +
            $"Pass={passed}");
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
