using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CobblemonLauncher.Models;

namespace CobblemonLauncher.Services
{
    public class GiftcodeRedeemResult
    {
        public int Coins { get; set; }
        public List<string> PermanentSkins { get; set; } = new();
        public List<string> TrialSkins { get; set; } = new();

        public string BuildSummaryMessage()
        {
            var parts = new List<string>();
            if (Coins > 0)
            {
                parts.Add($"• +{Coins:N0} Đá Cuội 🪨");
            }
            foreach (var s in PermanentSkins)
            {
                parts.Add($"• Skin Vĩnh Viễn: {s}");
            }
            foreach (var t in TrialSkins)
            {
                parts.Add($"• Skin Dùng Thử: {t}");
            }

            if (parts.Count == 0) return "Mã quà không chứa phần thưởng nào.";
            return string.Join("\n", parts);
        }
    }

    public static class GiftcodeService
    {
        public static string EncryptGiftcode(GiftcodePayload payload)
        {
            return GiftcodeCore.EncryptGiftcode(payload);
        }

        public static GiftcodePayload DecryptGiftcode(string code)
        {
            return GiftcodeCore.DecryptGiftcode(code);
        }

        public static async Task<GiftcodeRedeemResult> RedeemGiftcodeAsync(AppConfig config, string code, string gameDir)
        {
            var payload = GiftcodeCore.DecryptGiftcode(code);

            if (config.RedeemedGiftcodes != null && config.RedeemedGiftcodes.Contains(payload.CodeId, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Mã Giftcode này đã được bạn đổi trước đó rồi!");
            }

            if (payload.ExpiresAtUnix.HasValue)
            {
                long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (nowUnix > payload.ExpiresAtUnix.Value)
                {
                    throw new InvalidOperationException("Mã Giftcode này đã hết hạn sử dụng!");
                }
            }

            var result = new GiftcodeRedeemResult
            {
                Coins = payload.Coins
            };

            config.CobbleCoins += payload.Coins;

            var allSkins = SkinService.GetEffectiveSkins(null, gameDir);

            if (config.OwnedSkinIds == null) config.OwnedSkinIds = new();
            if (config.TrialSkins == null) config.TrialSkins = new();

            // 1. Permanent Skins
            foreach (var skinId in payload.PermanentSkins)
            {
                if (!config.OwnedSkinIds.Contains(skinId, StringComparer.OrdinalIgnoreCase))
                {
                    config.OwnedSkinIds.Add(skinId);
                }
                config.TrialSkins.Remove(skinId);

                var skinObj = allSkins.FirstOrDefault(s => string.Equals(s.Id, skinId, StringComparison.OrdinalIgnoreCase));
                string skinName = skinObj?.Name ?? skinId;
                result.PermanentSkins.Add(skinName);

                if (skinObj != null)
                {
                    await SkinService.InstallYsmModelAsync(skinObj, gameDir);
                }
            }

            // 2. Trial Skins
            foreach (var trial in payload.TrialSkins)
            {
                if (!config.OwnedSkinIds.Contains(trial.SkinId, StringComparer.OrdinalIgnoreCase) || config.TrialSkins.ContainsKey(trial.SkinId))
                {
                    if (!config.OwnedSkinIds.Contains(trial.SkinId, StringComparer.OrdinalIgnoreCase))
                    {
                        config.OwnedSkinIds.Add(trial.SkinId);
                    }
                    config.TrialSkins[trial.SkinId] = DateTime.UtcNow.AddHours(trial.Hours);

                    var skinObj = allSkins.FirstOrDefault(s => string.Equals(s.Id, trial.SkinId, StringComparison.OrdinalIgnoreCase));
                    string skinName = skinObj?.Name ?? trial.SkinId;
                    string durationText = trial.Hours >= 24 && Math.Abs(trial.Hours % 24) < 0.01
                        ? $"{(int)(trial.Hours / 24)} ngày"
                        : $"{trial.Hours:0.#} giờ";
                    result.TrialSkins.Add($"{skinName} (dùng thử {durationText})");

                    if (skinObj != null)
                    {
                        await SkinService.InstallYsmModelAsync(skinObj, gameDir);
                    }
                }
            }

            if (config.RedeemedGiftcodes == null) config.RedeemedGiftcodes = new();
            config.RedeemedGiftcodes.Add(payload.CodeId);
            config.Save();

            return result;
        }
    }
}
