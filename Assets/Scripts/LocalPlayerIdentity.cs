using System;
using UnityEngine;

public static class LocalPlayerIdentity
{
    private const string PlayerIdKey =
        "GameNetworkLab.PersistentPlayerId";

    public static string GetOrCreatePlayerId()
    {
        string existing =
            PlayerPrefs.GetString(PlayerIdKey, string.Empty);

        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        string created =
            $"player-{Guid.NewGuid():N}";

        PlayerPrefs.SetString(PlayerIdKey, created);
        PlayerPrefs.Save();

        Debug.Log(
            $"새 고정 PlayerId를 생성했습니다: {created}");

        return created;
    }
}
