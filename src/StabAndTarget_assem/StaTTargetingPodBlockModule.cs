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

        public ConfigurableJoint Joint;
        public bool FindConnected;

        private Quaternion zeroWorldRotation;        // 砲塔の初期ワールド姿勢
        private Quaternion zeroConnectedRotation;    // 接続元（機体）の初期ワールド姿勢

        // 射角限界（±60°）
        public float angleLimit = 75f;

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
            damper = DamperSlider.Value * 10000;

            ActivateKey = GetKey(Module.ActivateKey);

            IsEnemyToggle = GetToggle(Module.IsEnemyToggle);
            isEnemy = IsEnemyToggle.IsActive;

            Joint = GetComponent<ConfigurableJoint>();
            rigidbody = GetComponent<Rigidbody>();

            SetupJoint();

            //シミュ開始時のブロックのワールド姿勢を基準として保存
            zeroWorldRotation = rigidbody.rotation;

            Mod.Log(zeroWorldRotation.ToString());

            //接続先を取得、接続されてなければNoConnected
            FindConnected = GetConnectedRotation(out zeroConnectedRotation);


            BlockPlayerID = BlockBehaviour.ParentMachine.PlayerID;
            playerTargetingInfo = StaTTargetControllerHost.PlayerTargetingInfoList[BlockPlayerID];
        }

        public override void SimulateFixedUpdateHost()
        {
            base.SimulateFixedUpdateHost();

            //接続が無ければスルー
            if (!FindConnected || Joint == null)
            {
                return;
            }

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

                        Mod.Log("Current target is " + targetRigidbody.position);
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

                //照準を向ける
                ApplyAim(TargetPositionVector - transform.position);



            }
        }

        /// <summary>
        /// Jointを設定する関数（全軸ワールド基準、ロール固定、JointDrive）
        /// </summary>
        public void SetupJoint()
        {
            // ピッチ(X)・ヨー(Y)は自由、ロール(Z)は固定
            Joint.angularXMotion = ConfigurableJointMotion.Free;   // ピッチ
            Joint.angularYMotion = ConfigurableJointMotion.Free;   // ヨー
            Joint.angularZMotion = ConfigurableJointMotion.Free; // ロール固定

            Joint.axis = Vector3.right;          // (1, 0, 0)
            Joint.secondaryAxis = new Vector3(0f, 1f, 0f);

            // 全軸ワールド座標基準
            Joint.configuredInWorldSpace = false;

            // Slerpで全回転をまとめて制御
            Joint.rotationDriveMode = RotationDriveMode.Slerp;

            // JointDrive（positionSpring=P項, positionDamper=D項）
            JointDrive drive = new JointDrive
            {
                positionSpring = power,
                positionDamper = damper,
                maximumForce = Mathf.Infinity
            };
            Joint.slerpDrive = drive;
        }

        /// <summary>
        /// 根本接続が繋がるブロックの初期の姿勢を取得する関数。
        /// </summary>
        public bool GetConnectedRotation(out Quaternion connectedRotation)
        {
            Rigidbody hitRigidbody;

            //根本接続判定と同じ大きさの球で判定を取る
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z) * 0.16f, addingPointLayerMask);
            
            if(hitColliders.Length == 0)
            {
                connectedRotation = Quaternion.identity;
                return false;
            }
            else
            {
                foreach (Collider col in hitColliders)
                {
                    GameObject go = col.gameObject;

                    //3つ上の階層まで探る
                    for (int i = 0; i < 3; i++)
                    {
                        hitRigidbody = go.GetComponent<Rigidbody>();

                        if(hitRigidbody != null && hitRigidbody != rigidbody)
                        {
                            connectedRotation = hitRigidbody.rotation;

                            return true;
                        }

                        go = go.transform.parent.gameObject;

                        //マシン全体の親まで来たらそのコライダーは無視
                        if(go.name == "Simulation Machine")
                        {
                            break;
                        }
                    }
                }

                connectedRotation = Quaternion.identity;
                return false;
            }
        }

        /// <summary>
        /// 目標ワールド姿勢を、機体（接続元）の姿勢を基準とした相対回転に変換してtargetRotationに設定する。
        /// 機体の初期姿勢と現在姿勢の両方を基準変換に含めることで、
        /// 機体がどんな姿勢（大きな傾きを含む）でも正しく照準できるようにする。
        /// </summary>
        public void ApplyAim(Vector3 targetDir)
        {
            if (targetDir.sqrMagnitude < 0.0001f) return;

            // 接続元（機体）の現在のワールド回転
            Quaternion currentConnectedRot = Joint.connectedBody.rotation;

            // 機体の開始時からの旋回分
            Quaternion connectedDelta = currentConnectedRot * Quaternion.Inverse(zeroConnectedRotation);

            // 機体旋回を反映した基準姿勢と、その前方向
            Quaternion currentZero = connectedDelta * zeroWorldRotation;
            Vector3 baseForward = currentZero * Vector3.forward;    //(0,0,1)をcurrentZero方向に向けたベクトル

            Vector3 aim = targetDir.normalized;

            // --- ヨー角（水平面投影 + ワールド鉛直軸基準）---
            Vector3 baseForwardFlat = Vector3.ProjectOnPlane(baseForward, Vector3.up).normalized;
            Vector3 aimFlat = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;

            float yawAngle = 0f;
            if (baseForwardFlat.sqrMagnitude > 0.0001f && aimFlat.sqrMagnitude > 0.0001f)
            {
                yawAngle = Mod.SignedAngle(baseForwardFlat, aimFlat, Vector3.up);
            }

            // --- ピッチ角（仰角の差）---
            float aimPitch = Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
            float basePitch = Mathf.Asin(Mathf.Clamp(baseForward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float pitchAngle = aimPitch - basePitch;

            // --- 射角制限 ---
            yawAngle = Mathf.Clamp(yawAngle, -angleLimit, angleLimit);
            pitchAngle = Mathf.Clamp(pitchAngle, -angleLimit, angleLimit);

            // --- ヨー・ピッチを明示的な軸で作る ---
            Quaternion yawRot = Quaternion.AngleAxis(yawAngle, Vector3.up);
            Vector3 yawedForward = yawRot * baseForwardFlat;
            Vector3 pitchAxis = Vector3.Cross(yawedForward, Vector3.up).normalized;
            Quaternion pitchRot = Quaternion.AngleAxis(pitchAngle, pitchAxis);

            // --- 目標ワールド姿勢 ---
            Quaternion targetWorldRot = pitchRot * yawRot * currentZero;

            // --- 機体の姿勢を基準とした相対回転に変換 ---

            // 目標を、機体の現在姿勢基準の相対に変換
            Quaternion targetRelativeToConnected = Quaternion.Inverse(currentConnectedRot) * targetWorldRot;
            // 初期の「機体→砲塔」相対姿勢を基準にする
            Quaternion initialRelative = Quaternion.Inverse(zeroConnectedRotation) * zeroWorldRotation;
            // 初期相対からの差分を求める
            Quaternion deltaRot = Quaternion.Inverse(initialRelative) * targetRelativeToConnected;

            Joint.targetRotation = Quaternion.Inverse(deltaRot);

            if (rigidbody.IsSleeping())
            {
                rigidbody.WakeUp();
            }
        }

        /// <summary>
        /// 方向ベクトルを、基準方向からの角度がlimit以内になるようクランプする関数
        /// </summary>
        public static Vector3 ClampDirection(Vector3 reference, Vector3 dir, float limit)
        {
            // 基準方向と目標方向のなす角
            float angle = Vector3.Angle(reference, dir);

            // 制限以内ならそのまま
            if (angle <= limit) return dir;

            // 制限を超える場合、基準方向からlimit°の位置まで戻す
            // 基準方向とdirを含む平面上で、limit°の方向を作る
            Vector3 axis = Vector3.Cross(reference, dir).normalized;
            if (axis.sqrMagnitude < 0.0001f)
            {
                // 完全に反対方向など、軸が定まらない場合は基準方向を返す
                return reference;
            }
            return Quaternion.AngleAxis(limit, axis) * reference;
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