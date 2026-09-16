using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StaTSpace
{
    /// <summary>
    /// StabBaseの登録・読み出しを行う関数をまとめたクラス。
    /// 各StabBaseは、"(各ブロックの持つStabID)_(各ブロックの持ち主のID = BlockPlayerID)"というキーで保存される。 
    /// </summary>
    public static class StatTStabIDContoroller
    {
        //二重登録防止のためのDictionary
        private static Dictionary<BlockBehaviour, string> assigned = new Dictionary<BlockBehaviour, string>();

        /// <summary>
        /// IDを発行し、StabBaseブロックの登録を行う関数。
        /// StabBaseModuleBehaviour.OnSimulateStart から呼ぶ。
        /// </summary>
        public static void RegisterStabBase(BlockBehaviour bb, int stabID, int blockPlayerID)
        {
            //StabIDとBlockPlayerIDを繋げたID用の文字列を発行（例："0_1"）
            string id = stabID.ToString() + "_" + blockPlayerID.ToString();

            if (bb == null) return;   //nullの時はどうでもいいので0を返す

            if (assigned.ContainsKey(bb))
            {
                Mod.Warning(Mod.isJapanese ? "複数のStabBaseブロックが同じIDを保有しています。" : "Several StabBase has same ID");

                return;   // 二重発番防止、番号を書き換えないように元の番号を返す
            }

            //登録防止用のDictionaryに登録
            assigned.Add(bb, id);

            //IDとTransformを登録
            Mod.StabBaseDict.Add(id, bb.transform);
        }

        /// <summary>
        /// 辞書を消す関数。
        /// StabBaseModuleBehaviour.OnSimulateStop から呼ぶ。
        /// </summary>
        public static void ClearDictionary()
        {
            assigned.Clear();

            Mod.StabBaseDict.Clear();
        }
    }
}
