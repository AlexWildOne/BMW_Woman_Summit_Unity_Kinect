using System;
using UnityEngine;
using Windows.Kinect;
using Body = Windows.Kinect.Body;
using Joint = Windows.Kinect.Joint;

public class KinectBodyReader : MonoBehaviour
{
    [Header("Referências")]
    public KinectInitializer kinect;

    [Header("Estado de tracking")]
    public bool isTracked;
    public bool hasBody;
    public Body trackedBody;

    [Header("Seleção de corpo, estabilidade")]
    [Tooltip("Mantém o mesmo body por este tempo mínimo, evita saltos entre pessoas.")]
    public float trackedBodyStickTime = 1.2f;

    [Tooltip("Se o body atual desaparecer, quanto tempo ainda o tentamos manter antes de trocar.")]
    public float trackedBodyLostGrace = 0.35f;

    [Tooltip("Mínimo de joints com TrackingState.Tracked para aceitar um body como válido.")]
    public int minTrackedJointsForValidBody = 8;

    float trackedBodyStickTimer = 0f;
    float trackedBodyLostTimer = 0f;
    ulong trackedBodyId = 0;

    [Header("Suavização (menor = mais rápido, maior = mais estável)")]
    public float smoothTimeHead = 0.08f;
    public float smoothTimeTorso = 0.07f;
    public float smoothTimeHands = 0.05f;
    public float smoothTimeLegs = 0.06f;

    [Header("Cabeça e tronco")]
    public Vector3 headPos;
    public Vector3 spineBasePos;
    public Vector3 spineMidPos;
    public Vector3 spineShoulderPos;

    [Header("Ombros e braços")]
    public Vector3 shoulderLeftPos;
    public Vector3 shoulderRightPos;
    public Vector3 elbowLeftPos;
    public Vector3 elbowRightPos;
    public Vector3 wristLeftPos;
    public Vector3 wristRightPos;
    public Vector3 handLeftPos;
    public Vector3 handRightPos;

    [Header("Ancas, pernas e pés")]
    public Vector3 hipLeftPos;
    public Vector3 hipRightPos;
    public Vector3 kneeLeftPos;
    public Vector3 kneeRightPos;
    public Vector3 ankleLeftPos;
    public Vector3 ankleRightPos;
    public Vector3 footLeftPos;
    public Vector3 footRightPos;

    [Header("Power pose")]
    public bool powerPose;
    public float powerOffset = 0.15f;

    [Tooltip("Se true, exige TrackingState.Tracked (não aceita Inferred) para a power pose.")]
    public bool strictPowerPoseTracking = true;

    [Header("Gestos, Mala")]
    [Tooltip("Trigger (1 frame) quando o joelho direito levanta o suficiente para pedir abertura da mala.")]
    public bool trunkKickTriggered;

    [Tooltip("Tempo que o joelho tem de ficar acima do limiar para validar o gesto.")]
    public float trunkKickHoldTime = 0.25f;

    [Tooltip("Cooldown após disparar o gesto, evita spam e falsos positivos.")]
    public float trunkKickCooldown = 1.2f;

    [Tooltip("Limiar em metros, relativo à anca direita: KneeRight.y > HipRight.y + deltaY.")]
    public float trunkKickDeltaY = 0.14f;

    [Tooltip("Histerese: depois de levantar, tem de descer abaixo deste delta para rearmar. Normalmente 0.06 a 0.10.")]
    public float trunkKickReleaseDeltaY = 0.08f;

    [Tooltip("Se true, gestos só usam joints com TrackingState.Tracked, não aceita Inferred.")]
    public bool strictGestureTracking = true;

    [Header("Sinais de intenção, para evitar conflitos")]
    [Tooltip("Margem extra para considerar mão acima da cabeça (m).")]
    public float handAboveHeadMargin = 0.00f;

    [Tooltip("Margem extra para considerar mão acima do peito (SpineShoulder) para zoom (m).")]
    public float handAboveSpineShoulderMargin = 0.04f;

    [Tooltip("Margem extra para considerar mãos à frente do tronco (SpineMid) para zoom (m).")]
    public float handsInFrontOfSpineMidMargin = 0.06f;

    [Tooltip("Distância mínima entre mãos para considerar pose de zoom (m).")]
    public float zoomMinHandsDistance = 0.20f;

    [Header("Gesto de cor, dedicado")]
    [Tooltip("Se true, o gesto de cor só conta se a mão direita estiver baixa (evita conflito com rotação e zoom).")]
    public bool colorGestureUseDedicatedPose = true;

    [Tooltip("Mão direita tem de estar abaixo do SpineMid por este offset (m). Exemplo: -0.05.")]
    public float colorGestureRightHandBelowSpineMidOffset = -0.05f;

    [Header("Prioridades e bloqueios")]
    [Tooltip("Se true, quando zoom está ativo, bloqueia rotação e cor.")]
    public bool zoomHasPriority = true;

    [Tooltip("Se true, quando rotação está ativa, bloqueia cor.")]
    public bool rotationBlocksColor = true;

    [Header("Outputs de gesto (uso no BMWCarController)")]
    public bool leftHandAboveHead;
    public bool rightHandAboveHead;

    [Tooltip("Intenção: mudar cor.")]
    public bool colorGestureActive;

    [Tooltip("Intenção: rotação.")]
    public bool rotationGestureActive;

    [Tooltip("Intenção: zoom.")]
    public bool zoomGestureActive;

    // Eventos opcionais
    public event Action OnTrunkKick;
    public event Action<bool> OnPowerPoseChanged;

    // Velocidades de suavização
    private Vector3 v_head, v_spineBase, v_spineMid, v_spineShoulder;
    private Vector3 v_shL, v_shR, v_elL, v_elR, v_wrL, v_wrR, v_hL, v_hR;
    private Vector3 v_hipL, v_hipR, v_kneeL, v_kneeR, v_ankleL, v_ankleR, v_footL, v_footR;

    // Estado interno do gesto mala
    private float trunkKickHoldTimer;
    private float trunkKickCooldownTimer;
    private bool trunkKickArmed = true;

    // Cache power pose para evento
    private bool lastPowerPose = false;

    void Update()
    {
        trunkKickTriggered = false;

        if (!HasValidBodies())
        {
            ClearState();
            return;
        }

        UpdateTrackingState();
    }

    private void UpdateTrackingState()
    {
        isTracked = false;
        hasBody = false;
        trackedBody = null;

        Body selected = SelectStableBody();

        if (selected == null || !selected.IsTracked)
        {
            HandleBodyLost();
            return;
        }

        trackedBody = selected;
        isTracked = true;
        hasBody = true;

        trackedBodyId = selected.TrackingId;
        trackedBodyStickTimer = Mathf.Max(0f, trackedBodyStickTimer - Time.deltaTime);
        trackedBodyLostTimer = 0f;

        // Joints com smoothing
        headPos = DampTrackedJoint(headPos, selected, JointType.Head, ref v_head, smoothTimeHead, allowInferred: true);

        spineBasePos = DampTrackedJoint(spineBasePos, selected, JointType.SpineBase, ref v_spineBase, smoothTimeTorso, allowInferred: true);
        spineMidPos = DampTrackedJoint(spineMidPos, selected, JointType.SpineMid, ref v_spineMid, smoothTimeTorso, allowInferred: true);
        spineShoulderPos = DampTrackedJoint(spineShoulderPos, selected, JointType.SpineShoulder, ref v_spineShoulder, smoothTimeTorso, allowInferred: true);

        shoulderLeftPos = DampTrackedJoint(shoulderLeftPos, selected, JointType.ShoulderLeft, ref v_shL, smoothTimeTorso, allowInferred: true);
        shoulderRightPos = DampTrackedJoint(shoulderRightPos, selected, JointType.ShoulderRight, ref v_shR, smoothTimeTorso, allowInferred: true);

        elbowLeftPos = DampTrackedJoint(elbowLeftPos, selected, JointType.ElbowLeft, ref v_elL, smoothTimeHands, allowInferred: true);
        elbowRightPos = DampTrackedJoint(elbowRightPos, selected, JointType.ElbowRight, ref v_elR, smoothTimeHands, allowInferred: true);
        wristLeftPos = DampTrackedJoint(wristLeftPos, selected, JointType.WristLeft, ref v_wrL, smoothTimeHands, allowInferred: true);
        wristRightPos = DampTrackedJoint(wristRightPos, selected, JointType.WristRight, ref v_wrR, smoothTimeHands, allowInferred: true);
        handLeftPos = DampTrackedJoint(handLeftPos, selected, JointType.HandLeft, ref v_hL, smoothTimeHands, allowInferred: true);
        handRightPos = DampTrackedJoint(handRightPos, selected, JointType.HandRight, ref v_hR, smoothTimeHands, allowInferred: true);

        hipLeftPos = DampTrackedJoint(hipLeftPos, selected, JointType.HipLeft, ref v_hipL, smoothTimeLegs, allowInferred: true);
        hipRightPos = DampTrackedJoint(hipRightPos, selected, JointType.HipRight, ref v_hipR, smoothTimeLegs, allowInferred: true);
        kneeLeftPos = DampTrackedJoint(kneeLeftPos, selected, JointType.KneeLeft, ref v_kneeL, smoothTimeLegs, allowInferred: true);
        kneeRightPos = DampTrackedJoint(kneeRightPos, selected, JointType.KneeRight, ref v_kneeR, smoothTimeLegs, allowInferred: true);
        ankleLeftPos = DampTrackedJoint(ankleLeftPos, selected, JointType.AnkleLeft, ref v_ankleL, smoothTimeLegs, allowInferred: true);
        ankleRightPos = DampTrackedJoint(ankleRightPos, selected, JointType.AnkleRight, ref v_ankleR, smoothTimeLegs, allowInferred: true);
        footLeftPos = DampTrackedJoint(footLeftPos, selected, JointType.FootLeft, ref v_footL, smoothTimeLegs, allowInferred: true);
        footRightPos = DampTrackedJoint(footRightPos, selected, JointType.FootRight, ref v_footR, smoothTimeLegs, allowInferred: true);

        // Power pose
        powerPose = IsPowerPose(selected);
        if (powerPose != lastPowerPose)
        {
            lastPowerPose = powerPose;
            OnPowerPoseChanged?.Invoke(powerPose);
        }

        // Intenções
        UpdateIntentSignals();

        // Gesto mala
        UpdateTrunkKickGesture(selected);
    }

    private void HandleBodyLost()
    {
        trackedBodyLostTimer += Time.deltaTime;

        if (trackedBodyLostTimer < trackedBodyLostGrace)
        {
            isTracked = false;
            hasBody = false;
            trackedBody = null;

            powerPose = false;
            leftHandAboveHead = false;
            rightHandAboveHead = false;
            colorGestureActive = false;
            rotationGestureActive = false;
            zoomGestureActive = false;

            if (lastPowerPose != false)
            {
                lastPowerPose = false;
                OnPowerPoseChanged?.Invoke(false);
            }

            return;
        }

        ClearState();
    }

    // ===================== Intenções com prioridade, sem overlaps =====================
    private void UpdateIntentSignals()
    {
        leftHandAboveHead = false;
        rightHandAboveHead = false;
        colorGestureActive = false;
        rotationGestureActive = false;
        zoomGestureActive = false;

        if (!IsValid(headPos) || !IsValid(handLeftPos) || !IsValid(handRightPos) ||
            !IsValid(shoulderLeftPos) || !IsValid(shoulderRightPos) ||
            !IsValid(spineMidPos) || !IsValid(spineShoulderPos))
            return;

        // Base signals
        leftHandAboveHead = handLeftPos.y > (headPos.y + handAboveHeadMargin);
        rightHandAboveHead = handRightPos.y > (headPos.y + handAboveHeadMargin);

        // ZOOM
        float handsDist = Vector3.Distance(handLeftPos, handRightPos);

        bool handsAboveChest =
            (handLeftPos.y > spineShoulderPos.y + handAboveSpineShoulderMargin) &&
            (handRightPos.y > spineShoulderPos.y + handAboveSpineShoulderMargin);

        bool handsInFront =
            (handLeftPos.z > spineMidPos.z + handsInFrontOfSpineMidMargin) &&
            (handRightPos.z > spineMidPos.z + handsInFrontOfSpineMidMargin);

        bool handsApartEnough = handsDist >= zoomMinHandsDistance;

        bool zoomCandidate = handsAboveChest && handsInFront && handsApartEnough;

        // ROTATION (intenção leve)
        float shoulderYAvg = 0.5f * (shoulderLeftPos.y + shoulderRightPos.y);

        bool handsInRotationBand =
            (handLeftPos.y > shoulderYAvg - 0.05f) ||
            (handRightPos.y > shoulderYAvg - 0.05f);

        bool armsOpen =
            (handRightPos.x - shoulderRightPos.x) > 0.05f ||
            (handLeftPos.x - shoulderLeftPos.x) < -0.05f;

        bool rotationCandidate = handsInRotationBand && armsOpen;

        // COLOR (dedicado)
        bool colorCandidate = leftHandAboveHead;

        if (colorGestureUseDedicatedPose)
        {
            bool rightLow = handRightPos.y < spineMidPos.y + colorGestureRightHandBelowSpineMidOffset;
            colorCandidate = leftHandAboveHead && rightLow;
        }

        // PRIORIDADES
        if (zoomHasPriority && zoomCandidate)
        {
            zoomGestureActive = true;
            rotationGestureActive = false;
            colorGestureActive = false;
            return;
        }

        rotationGestureActive = rotationCandidate;

        if (rotationBlocksColor && rotationGestureActive)
        {
            colorGestureActive = false;
        }
        else
        {
            colorGestureActive = colorCandidate;
        }

        zoomGestureActive = false;
    }

    // ===================== Seleção de corpo estável =====================
    private Body SelectStableBody()
    {
        if (kinect == null || kinect.bodies == null) return null;

        if (trackedBodyId != 0)
        {
            Body same = FindBodyById(trackedBodyId);
            if (IsBodyValid(same))
            {
                trackedBodyStickTimer = trackedBodyStickTime;
                return same;
            }
        }

        if (trackedBodyStickTimer > 0f && trackedBodyId != 0)
        {
            Body same = FindBodyById(trackedBodyId);
            if (same != null && same.IsTracked)
                return same;
        }

        Body best = null;
        int bestScore = -1;

        foreach (var b in kinect.bodies)
        {
            if (!IsBodyValid(b)) continue;

            int score = CountTrackedJoints(b);
            if (score > bestScore)
            {
                bestScore = score;
                best = b;
            }
        }

        if (best != null)
        {
            trackedBodyId = best.TrackingId;
            trackedBodyStickTimer = trackedBodyStickTime;
            trackedBodyLostTimer = 0f;
        }

        return best;
    }

    private Body FindBodyById(ulong id)
    {
        if (id == 0 || kinect == null || kinect.bodies == null) return null;

        foreach (var b in kinect.bodies)
        {
            if (b != null && b.IsTracked && b.TrackingId == id)
                return b;
        }

        return null;
    }

    private bool IsBodyValid(Body b)
    {
        if (b == null || !b.IsTracked) return false;

        int trackedCount = CountTrackedJoints(b);
        return trackedCount >= Mathf.Max(1, minTrackedJointsForValidBody);
    }

    private int CountTrackedJoints(Body b)
    {
        if (b == null) return 0;

        int count = 0;
        foreach (var kv in b.Joints)
        {
            if (kv.Value.TrackingState == TrackingState.Tracked)
                count++;
        }
        return count;
    }

    // ===================== Gestos =====================
    private void UpdateTrunkKickGesture(Body body)
    {
        if (body == null || !body.IsTracked)
        {
            trunkKickHoldTimer = 0f;
            trunkKickCooldownTimer = 0f;
            trunkKickArmed = true;
            return;
        }

        if (trunkKickCooldownTimer > 0f)
        {
            trunkKickCooldownTimer -= Time.deltaTime;
            trunkKickHoldTimer = 0f;
            return;
        }

        Joint knee = body.Joints[JointType.KneeRight];
        Joint hip = body.Joints[JointType.HipRight];

        if (!IsJointAcceptedForGesture(knee) || !IsJointAcceptedForGesture(hip))
        {
            trunkKickHoldTimer = 0f;
            return;
        }

        float kneeY = knee.Position.Y;
        float hipY = hip.Position.Y;
        float delta = kneeY - hipY;

        bool isLift = delta > trunkKickDeltaY;
        bool isReleased = delta < trunkKickReleaseDeltaY;

        if (!trunkKickArmed)
        {
            if (isReleased)
                trunkKickArmed = true;

            trunkKickHoldTimer = 0f;
            return;
        }

        if (isLift)
        {
            trunkKickHoldTimer += Time.deltaTime;

            if (trunkKickHoldTimer >= trunkKickHoldTime)
            {
                trunkKickTriggered = true;
                OnTrunkKick?.Invoke();

                trunkKickCooldownTimer = Mathf.Max(0.05f, trunkKickCooldown);
                trunkKickHoldTimer = 0f;
                trunkKickArmed = false;
            }
        }
        else
        {
            trunkKickHoldTimer = 0f;
        }
    }

    private bool IsPowerPose(Body body)
    {
        if (body == null || !body.IsTracked) return false;

        Joint head = body.Joints[JointType.Head];
        Joint handR = body.Joints[JointType.HandRight];

        if (!IsJointAcceptedForPowerPose(head) || !IsJointAcceptedForPowerPose(handR))
            return false;

        float headY = head.Position.Y;
        float handY = handR.Position.Y;

        return handY > headY + powerOffset;
    }

    // ===================== Auxiliares de joints =====================
    private Vector3 DampTrackedJoint(
        Vector3 current,
        Body body,
        JointType type,
        ref Vector3 velocity,
        float smoothTime,
        bool allowInferred)
    {
        if (body == null) return current;

        Joint joint = body.Joints[type];

        if (!IsJointTrackedOrInferred(joint, allowInferred))
            return current;

        Vector3 target = ToUnityVector(joint.Position);
        return Damp(current, target, ref velocity, smoothTime);
    }

    private bool IsJointTrackedOrInferred(Joint joint, bool allowInferred)
    {
        if (allowInferred)
            return joint.TrackingState == TrackingState.Tracked || joint.TrackingState == TrackingState.Inferred;

        return joint.TrackingState == TrackingState.Tracked;
    }

    private bool IsJointAcceptedForGesture(Joint joint)
    {
        if (strictGestureTracking)
            return joint.TrackingState == TrackingState.Tracked;

        return joint.TrackingState == TrackingState.Tracked || joint.TrackingState == TrackingState.Inferred;
    }

    private bool IsJointAcceptedForPowerPose(Joint joint)
    {
        if (strictPowerPoseTracking)
            return joint.TrackingState == TrackingState.Tracked;

        return joint.TrackingState == TrackingState.Tracked || joint.TrackingState == TrackingState.Inferred;
    }

    private Vector3 ToUnityVector(CameraSpacePoint p)
    {
        return new Vector3(p.X, p.Y, -p.Z);
    }

    private Vector3 Damp(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime)
    {
        if (float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsNaN(target.z))
            return current;

        smoothTime = Mathf.Max(0.001f, smoothTime);
        return Vector3.SmoothDamp(current, target, ref velocity, smoothTime);
    }

    private bool IsValid(Vector3 v)
    {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return false;
        return true;
    }

    private void ClearState()
    {
        isTracked = false;
        hasBody = false;
        trackedBody = null;

        trackedBodyId = 0;
        trackedBodyStickTimer = 0f;
        trackedBodyLostTimer = 0f;

        powerPose = false;
        if (lastPowerPose != false)
        {
            lastPowerPose = false;
            OnPowerPoseChanged?.Invoke(false);
        }

        trunkKickTriggered = false;
        trunkKickHoldTimer = 0f;
        trunkKickCooldownTimer = 0f;
        trunkKickArmed = true;

        leftHandAboveHead = false;
        rightHandAboveHead = false;
        colorGestureActive = false;
        rotationGestureActive = false;
        zoomGestureActive = false;
    }

    private bool HasValidBodies()
    {
        return kinect != null && kinect.bodies != null && kinect.bodies.Length > 0;
    }
}
