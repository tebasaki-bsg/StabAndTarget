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

        [XmlElement("BulletSpeedSlider")]
        [RequireToValidate]
        public MSliderReference BulletSpeedSlider;

        [XmlElement("PowerSlider")]
        [RequireToValidate]
        public MSliderReference PowerSlider;

        [XmlElement("ActivateKey")]
        [RequireToValidate]
        public MKeyReference ActivateKey;
    }

    public class StaTTargetingPodBlockModuleBehaviour : BlockModuleBehaviour<StaTTargetingPodBlockModule>
    {
        public MSlider MaxDistanceSlider;
        public float maxDistance;
        public MSlider BulletSpeedSlider;
        public float bulletSpeed;
        public MSlider PowerSlider;
        public float power;
        public MKey ActivateKey;

        public MToggle AppearPDConfigToggle;
        public MSlider ProportionalSlider;
        public float proportional;
        public MSlider DerivativeSlider;
        public float derivative;

        public Rigidbody rigidbody;

        public Vector3 TargetPositionVector;

        public int BlockPlayerID = 0;
        public PlayerTargetingInfo playerTargetingInfo;

        public LayerMask layerMask = (1 << 0) | (1 << 12) | (1 << 14) | (1 << 25) | (1 << 26);

        public ConfigurableJoint Joint;

        // === ゼロ点（ジョイント初期化時のワールド姿勢）===
        private Quaternion zeroWorldRotation;

        // 射角限界（±60°）
        public float angleLimit = 60f;

        public override void SafeAwake()
        {
            base.SafeAwake();

            AppearPDConfigToggle = BlockBehaviour.AddToggle(Mod.isJapanese ? "設定を変更" : "PD config", "pd-config", false);
            AppearPDConfigToggle.DisplayInMapper = true;
            AppearPDConfigToggle.Toggled += AppearPDConfig;

            ProportionalSlider = BlockBehaviour.AddSlider(Mod.isJapanese ? "P成分" : "Propotional", "propotional", 10f, 0.0f, 10000f);
            ProportionalSlider.DisplayInMapper = false;
            DerivativeSlider = BlockBehaviour.AddSlider(Mod.isJapanese ? "D成分" : "Derivative", "derivative", 3f, 0.0f, 10000f);
            DerivativeSlider.DisplayInMapper = false;
        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            MaxDistanceSlider = GetSlider(Module.MaxDistanceSlider);
            maxDistance = MaxDistanceSlider.Value;
            BulletSpeedSlider = GetSlider(Module.BulletSpeedSlider);
            bulletSpeed = BulletSpeedSlider.Value;

            //パワーは100000倍にする
            PowerSlider = GetSlider(Module.PowerSlider);
            power = PowerSlider.Value * 100000f;

            ActivateKey = GetKey(Module.ActivateKey);

            Joint = GetComponent<ConfigurableJoint>();
            rigidbody = GetComponent<Rigidbody>();

            //P成分・D成分の設定、詳細設定がオンならスライダーから、オフならPower値から設定
            if (AppearPDConfigToggle.IsActive)
            {
                proportional = ProportionalSlider.Value * 100000f;
                derivative = DerivativeSlider.Value * 100000f;
            }
            else
            {
                proportional = power;
                derivative = power * 0.3f;
            }

            SetupJoint();

            // ジョイント初期化時のブロックのワールド姿勢を基準として保存
            zeroWorldRotation = rigidbody.rotation;

            BlockPlayerID = BlockBehaviour.ParentMachine.PlayerID;
            playerTargetingInfo = StaTTargetControllerHost.PlayerTargetingInfoList[BlockPlayerID];
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

            Joint.secondaryAxis = new Vector3(0f, 1f, 0f);

            // 全軸ワールド座標基準
            Joint.configuredInWorldSpace = true;

            // Slerpで全回転をまとめて制御
            Joint.rotationDriveMode = RotationDriveMode.Slerp;

            // JointDrive（positionSpring=P項, positionDamper=D項）
            JointDrive drive = new JointDrive
            {
                positionSpring = proportional,
                positionDamper = derivative,
                maximumForce = Mathf.Infinity
            };
            Joint.slerpDrive = drive;
        }

        public override void SimulateFixedUpdateHost()
        {
            base.SimulateFixedUpdateHost();

            if (Joint == null) return;

            // キーが押されているときのみ照準
            if (ActivateKey.IsHeld || ActivateKey.EmulationHeld())
            {
                // --- 狙う位置を決める ---
                if (!playerTargetingInfo.LockingSomething)
                {
                    if (Physics.Raycast(StaTTargetController.CamPosition, StaTTargetController.CamForward, out RaycastHit raycastHit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
                        TargetPositionVector = raycastHit.point;
                    else
                        TargetPositionVector = StaTTargetController.CamPosition + StaTTargetController.CamForward * maxDistance;
                }
                else if (playerTargetingInfo.LockState == StaTLockState.Primary)
                {
                    TargetPositionVector = playerTargetingInfo.CurrentAimRigidbody.transform.position;
                }
                else
                {
                    TargetPositionVector = PredictPosition(transform.position, playerTargetingInfo.CurrentAimRigidbody.transform.position, playerTargetingInfo.CurrentAimRigidbody, bulletSpeed);
                }

                // --- 照準を適用 ---
                ApplyAim(TargetPositionVector - transform.position);
            }
        }

        /// <summary>
        /// 狙う方向へJointのtargetRotationを設定する関数（ワールド基準）
        /// </summary>
        public void ApplyAim(Vector3 targetDir)
        {
            if (targetDir.sqrMagnitude < 0.0001f) return;

            // ゼロ点姿勢における「前方向（発射方向）」を基準にする
            Vector3 zeroForward = zeroWorldRotation * Vector3.forward;

            // 目標方向を±angleLimit以内にクランプする
            Vector3 clampedDir = ClampDirection(zeroForward, targetDir.normalized, angleLimit);

            // クランプ後の方向を向くワールド回転を作る（upはワールド上）
            // ±60°制限下なので特異点(真上/真下)には到達せず、LookRotationは安定
            Quaternion targetWorldRot = Quaternion.LookRotation(clampedDir, Vector3.up);

            // ゼロ点（初期姿勢）を基準とした相対回転に変換
            Quaternion deltaRot = targetWorldRot * Quaternion.Inverse(zeroWorldRotation);

            // ConfigurableJoint.targetRotationは反転して与える仕様
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

        public static Vector3 PredictPosition(Vector3 myPos, Vector3 enemyPos, Rigidbody enemyRb, float bulletSpd)
        {
            Vector3 toTarget = enemyPos - myPos;
            Vector3 enemyVel = enemyRb.velocity;
            float a = Vector3.Dot(enemyVel, enemyVel) - bulletSpd * bulletSpd;
            float b = 2f * Vector3.Dot(toTarget, enemyVel);
            float c = Vector3.Dot(toTarget, toTarget);
            float t = SolveInterceptTime(a, b, c);
            if (t <= 0f) return enemyPos;
            return enemyPos + enemyVel * t;
        }

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

        public void AppearPDConfig(bool value)
        {
            ProportionalSlider.DisplayInMapper = value;
            DerivativeSlider.DisplayInMapper = value;
        }
    }
}