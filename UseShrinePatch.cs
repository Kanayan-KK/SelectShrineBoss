using System.Linq;
using HarmonyLib;

namespace SelectShrineBoss
{
    [HarmonyPatch(typeof(TraitShrine), nameof(TraitShrine._OnUse))]
    internal class UseShrinePatch
    {
        private static bool Prefix(TraitShrine __instance, Chara c)
        {
            // 争いの祠以外の場合は処理終了
            if (__instance.Shrine.id != "strife")
                return true;

            // 自キャラの座標を取得
            var point = __instance.owner.ExistsOnMap ? __instance.owner.pos : EClass.pc.pos;

            // 自キャラ周辺の座標を取得
            var pos = point.GetNearestPoint(allowChara: false);

            // バイオームオブジェクト取得
            var biome = pos.cell.biome;

            // ボス用のスポーン設定を生成
            var spawnSettings = SpawnSetting.Boss(__instance.owner.LV);

            // 危険度取得
            var dangerLv = spawnSettings.dangerLv == -1 ? EClass._zone.DangerLv : spawnSettings.dangerLv;

            // 生成レベル取得
            var lv = spawnSettings.filterLv == -1 ? dangerLv : spawnSettings.filterLv;

            // 生成レベルに危険度を反映
            if (EClass._zone.ScaleType == ZoneScaleType.Void)
            {
                lv = ((dangerLv - 1) % 50 + 5) * 150 / 100;
                if (lv >= 20 && EClass.rnd(100) < lv)
                    lv = dangerLv;
            }

            // スポーン可能モンスターリストを取得
            var spawnList = SpawnList.Get(biome.spawn.GetRandomCharaId());

            // 生成レベルによってリストをフィルタリング
            var filteredList = spawnList.Filter(lv, spawnSettings.levelRange);

            // レベルでソート
            var sortedList = filteredList.rows.OrderByDescending(row => row.LV).ToList();

            // UI表示
            EClass.ui.AddLayer<LayerList>()
                .SetSize(400, -1)
                .SetList(sortedList, (r) => $"LV.{r.LV} {r.GetName()}", (index, text) =>
                {
                    // 選択モンスターをボスとしてスポーン  
                    var selectedRow = sortedList[index];
                    SpawnEnemy(pos, __instance, selectedRow);
                })
                .SetHeader("Select Boss");

            return false;
        }

        private static void SpawnEnemy(Point pos, TraitShrine shrine, CardRow bossRow)
        {
            var count = 3 + EClass.rnd(2);

            // ボスを生成
            EClass._zone
                .SpawnMob(pos,
                    SpawnSetting.Boss(bossRow.id, fixedLv: shrine.owner.LV))?.PlayEffect("teleport");

            // モブを生成
            for (var i = 1; i < count; i++)
            {
                EClass._zone
                    .SpawnMob(pos,
                        SpawnSetting.DefenseEnemy(shrine.owner.LV))?.PlayEffect("teleport");
            }
        }
    }
}