using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Modding;
using Modding.Modules;
using Modding.Serialization;
using Modding.Blocks;
using Modding.Common;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using UnityEngine.UI;
using Localisation;

namespace StaTSpace
{
    [XmlRoot("StaTTargetingPodBlockModule")]
    [Reloadable]
    public class StaTTargetingPodBlockModule : BlockModule
    {
        [XmlElement("MaxDistanceSlider")]
        [RequireToValidate]
        public MSliderReference MaxDistanceSlider;

        [XmlElement("MinDistanceSlider")]
        [RequireToValidate]
        public MSliderReference MinDistanceSlider;

        [XmlElement("BulletSpeedSlider")]
        [RequireToValidate]
        public MSliderReference BulletSpeedSlider;

        [XmlElement("PowerSlider")]
        [RequireToValidate]
        public MSliderReference PowerSlider;

        [XmlElement("DamperSlider")]
        [RequireToValidate]
        public MSliderReference DamperSlider;

        [XmlElement("ActivateKey")]
        [RequireToValidate]
        public MKeyReference ActivateKey;

        [XmlElement("IsEnemyToggle")]
        [RequireToValidate]
        public MToggleReference IsEnemyToggle;
    }

    public class StaTTargetingPodBlockModuleBehaviour : BlockModuleBehaviour<StaTTargetingPodBlockModule>
    {
        public MSlider MaxDistanceSlider;
        public float maxDistance;
        public MSlider MinDistanceSlider;
        public float minDistance;

        public MSlider BulletSpeedSlider;
        public float bulletSpeed;

        public MSlider PowerSlider;
        public float power;
        public MSlider DamperSlider;
        public float damper;

        public MKey ActivateKey;

        public MToggle IsEnemyToggle;
        public bool isEnemy;
        public int CheckDistanceTime = 0;

        public Rigidbody rigidbody;
        public Rigidbody targetRigidbody;

        public Vector3 TargetPositionVector;

        public int BlockPlayerID = 0;
        public PlayerTargetingInfo playerTargetingInfo;

        public LayerMask layerMask = (1 << 0) | (1 << 12) | (1 << 14) | (1 << 25) | (1 << 26);
        public LayerMask addingPointLayerMask = (1 << 12);

        public bool init = false;

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            //各種スライダーを取得
            MaxDistanceSlider = GetSlider(Module.MaxDistanceSlider);
            maxDistance = MaxDistanceSlider.Value;
            MinDistanceSlider = GetSlider(Module.MinDistanceSlider);
            minDistance = MinDistanceSlider.Value;
            BulletSpeedSlider = GetSlider(Module.BulletSpeedSlider);
            bulletSpeed = BulletSpeedSlider.Value;

            //P成分は100000倍にする
            PowerSlider = GetSlider(Module.PowerSlider);
            power = PowerSlider.Value * 100000f;

            //D成分は10000倍（Pの1/10）にする
            DamperSlider = GetSlider(Module.DamperSlider);
            damper = DamperSlider.Value;

            ActivateKey = GetKey(Module.ActivateKey);

            IsEnemyToggle = GetToggle(Module.IsEnemyToggle);
            isEnemy = IsEnemyToggle.IsActive;

            rigidbody = GetComponent<Rigidbody>();
            rigidbody.angularDrag = damper;
            rigidbody.inertiaTensor = new Vector3(10f, 10f, 10f);

            BlockPlayerID = BlockBehaviour.ParentMachine.PlayerID;
            playerTargetingInfo = StaTTargetControllerHost.PlayerTargetingInfoList[BlockPlayerID];
        }

        public override void SimulateFixedUpdateHost()
        {
            base.SimulateFixedUpdateHost();

            // キーが押されているときのみ照準
            if (ActivateKey.IsHeld || ActivateKey.EmulationHeld())
            {
                /// <summary>
                /// 目標のベクトルを取得する。
                /// 敵⇒最も近い距離のIFF
                /// ロック先が無い⇒カメラ
                /// </summary>

                if (isEnemy)
                {
                    if(StaTTargetController.IFFDictForEnemy.Count == 0)
                    {
                        return;
                    }

                    if(CheckDistanceTime < 10)
                    {
                        CheckDistanceTime++;
                    }
                    else
                    {
                        CheckDistanceTime = 0;

                        float sqrDistance = float.MaxValue;

                        //最短距離のRigidbodyを取得
                        foreach (IFFEntry iffEntry in StaTTargetController.IFFDictForEnemy.Values)
                        {
                            float thisSqrDistance = (iffEntry.IFFBehaviour.transform.position - transform.position).sqrMagnitude;
                            if(thisSqrDistance < sqrDistance)
                            {
                                targetRigidbody = iffEntry.Rigidbody;
                            }
                        }
                    }

                    if(targetRigidbody == null)
                    {
                        return;
                    }

                    //敵からは一次ロック
                    TargetPositionVector = targetRigidbody.position;
                }
                
                if (!playerTargetingInfo.LockingSomething)
                {
                    //カメラからRayを飛ばす。(カメラ座標 + カメラ方向 * 最小距離)がスタート地点
                    if (Physics.Raycast(StaTTargetController.CamPosition + StaTTargetController.CamForward * minDistance, StaTTargetController.CamForward, out RaycastHit raycastHit, maxDistance - minDistance, layerMask, QueryTriggerInteraction.Ignore))
                    {
                        TargetPositionVector = raycastHit.point;
                    }
                    
                    //何もなければ最大距離
                    else
                    {
                        TargetPositionVector = StaTTargetController.CamPosition + StaTTargetController.CamForward * maxDistance;
                    }
                        
                }
                //一次ロックは敵の座標そのまま
                else if (playerTargetingInfo.LockState == StaTLockState.Primary)
                {
                    TargetPositionVector = playerTargetingInfo.CurrentAimRigidbody.transform.position;
                }
                //二次ロックは弾速と敵の速度から偏差を加える
                else
                {
                    TargetPositionVector = PredictPosition(transform.position, playerTargetingInfo.CurrentAimRigidbody.transform.position, playerTargetingInfo.CurrentAimRigidbody, bulletSpeed);
                }

                if (rigidbody.IsSleeping())
                {
                    rigidbody.WakeUp();
                }

                //照準を向ける
                ApplyAim(TargetPositionVector - transform.position);

                
                    
            }
        }

        /// <summary>
        /// 目標ワールド姿勢を、機体（接続元）の姿勢を基準とした相対回転に変換してtargetRotationに設定する。
        /// 機体の初期姿勢と現在姿勢の両方を基準変換に含めることで、
        /// 機体がどんな姿勢（大きな傾きを含む）でも正しく照準できるようにする。
        /// </summary>
        public void ApplyAim(Vector3 targetDir)
        {
            rigidbody.rotation = Quaternion.LookRotation(targetDir);
        }

        /// <summary>
        /// 敵の速度と弾速から弾着時の未来位置を予測する関数
        /// </summary>
        public static Vector3 PredictPosition(Vector3 myPos, Vector3 enemyPos, Rigidbody enemyRb, float bulletSpd)
        {
            Vector3 toTarget = enemyPos - myPos;
            Vector3 enemyVel = enemyRb.velocity;

            //二次方程式の係数
            float a = Vector3.Dot(enemyVel, enemyVel) - bulletSpd * bulletSpd;
            float b = 2f * Vector3.Dot(toTarget, enemyVel);
            float c = Vector3.Dot(toTarget, toTarget);

            float t = SolveInterceptTime(a, b, c);
            if (t <= 0f) return enemyPos;
            return enemyPos + enemyVel * t;
        }

        /// <summary>
        /// at^2 + bt + c = 0を解く関数  
        /// </summary>
        public static float SolveInterceptTime(float a, float b, float c)
        {
            if (Mathf.Abs(a) < 0.001f)
            {
                if (Mathf.Abs(b) < 0.001f) return -1f;
                return -c / b;
            }
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f) return -1f;
            float sqrt = Mathf.Sqrt(discriminant);
            float invDenom = 1f / (2f * a);
            float t1 = (-b + sqrt) * invDenom;
            float t2 = (-b - sqrt) * invDenom;
            float t = Mathf.Min(t1, t2);
            if (t < 0f) t = Mathf.Max(t1, t2);
            return t;
        }
    }
}