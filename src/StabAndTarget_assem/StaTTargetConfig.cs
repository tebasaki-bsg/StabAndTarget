using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StaTSpace
{
    /// <summary>
    /// ロックオンの性能をまとめたコンフィグ用のクラス。
    /// modに応じてここを書き換えさせる。
    /// </summary>
    public static class StaTTargetConfig
    {
        //ロックオン可能距離, ロックオン可能範囲
        internal static float maxRange = 1200f * 1200f;
        internal static float lockAreaWidth = 0.18f;
        internal static float lockAreaHeight = 0.32f;

        //ロックオンの所要時間の最小・最大とその時の距離、これ以上/以下は所用時間が固定となる
        internal static float minLockTime = 0.3f;
        internal static float minLockTimeRange = 50f * 50f;
        internal static float maxLockTime = 1.5f;
        internal static float maxLockTimeRange = 1000f * 1000f;

        //ロックオンの所要時間を計算するための定数, （ロックオン所要時間) = LockTimeProportional * (距離)^2 + LockTimeConstant
        internal static float LockTimeProportional = 100 * (maxLockTime - minLockTime) / (minLockTimeRange * minLockTimeRange + maxLockTimeRange * maxLockTimeRange);
        internal static float LockTimeConstant = 100 * minLockTime - 100 * minLockTimeRange * minLockTimeRange * (maxLockTime - minLockTime) / (minLockTimeRange * minLockTimeRange + maxLockTimeRange * maxLockTimeRange);

        //各定数のゲッター
        public static float MaxRange
        {
            get
            {
                return maxRange;
            }
        }

        public static float LockAreaWidth
        {
            get
            {
                return lockAreaWidth;
            }
        }

        public static float LockAreaHeight
        {
            get
            {
                return lockAreaHeight;
            }
        }

        public static float MinLockTime
        {
            get
            {
                return minLockTime;
            }
        }

        public static float MinLockTimeRange
        {
            get
            {
                return minLockTimeRange;
            }
        }

        public static float MaxLockTime
        {
            get
            {
                return maxLockTime;
            }
        }

        public static float MaxLockTimeRange
        {
            get
            {
                return maxLockTimeRange;
            }
        }

        public static void ChangeMaxRange(float value)
        {
            if(maxLockTimeRange > value)
            {
                Mod.Error("MaxRange must be smaller than MaxLockTimeRange. You must change MaxLockTimeRange before.");
            }

            else
            {
                maxRange = value;
            }
        }

        public static void ChangeLockAreaWidth(float value)
        {
            lockAreaWidth = value;
        }

        public static void ChangeLockAreaHeight(float value)
        {
            lockAreaHeight = value;
        }

        public static void ChangeLockSpeed(float minTime, float minRange, float maxTime, float maxRange)
        {
            minLockTime = minTime;
            minLockTimeRange = minRange;
            maxLockTime = maxTime;
            maxLockTimeRange = maxRange;

            LockTimeProportional = 100 * (maxTime - minTime) / (minRange * minRange + maxRange * maxRange);
            LockTimeConstant = 100 * minTime - 100 * minRange * minRange * (maxTime - minTime) / (minRange * minRange + maxRange * maxRange);

        }
    }
}
