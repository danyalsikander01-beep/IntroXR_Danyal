using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Attach ONE instance to Left Controller and ONE instance to Right Controller.
/// Add a trigger collider (SphereCollider is easiest) to each controller object.
/// Add Rigidbody + Collider to grabbable object (e.g., Cube).
///
/// This implements HW2 Part 2:
/// - 1-hand grab using delta position + delta rotation
/// - rotates around controller origin (r2 - r term)
/// - 2-hand simultaneous manipulation by combining BOTH hands' deltas
/// - optional: double rotation (toggleable)
/// </summary>
public class CustomGrab : MonoBehaviour
{
    [Header("Setup")]
    public XRNode handNode = XRNode.LeftHand;
    public LayerMask grabbableLayers = ~0; // default: everything

    [Header("Optional Extra Credit")]
    public bool allowDoubleRotationToggle = true;

    // Hover / grab state (per controller)
    private Rigidbody _hoverRb;
    private Rigidbody _grabbedRb;

    private Vector3 _prevHandPos;
    private Quaternion _prevHandRot;
    private bool _hasPrevPose;

    private InputDevice _device;
    private bool _prevPrimaryButton;

    // Shared (static) state for combining BOTH hands each frame
    private class FrameState
    {
        public int frame;
        public Vector3 objPos0;
        public Quaternion objRot0;
        public HandDelta left;
        public HandDelta right;
    }

    private struct HandDelta
    {
        public bool active;
        public Vector3 pivotPos;
        public Vector3 deltaPos;
        public Quaternion deltaRot;
    }

    private static readonly Dictionary<Rigidbody, FrameState> _frameStates = new();
    private static readonly Dictionary<Rigidbody, int> _grabCounts = new();
    private static int _lastAppliedFrame = -1;

    private static bool _doubleRotationEnabled = false;

    private void Start()
    {
        _device = InputDevices.GetDeviceAtXRNode(handNode);
    }

    private void Update()
    {
        if (!_device.isValid)
            _device = InputDevices.GetDeviceAtXRNode(handNode);

        HandleDoubleRotationToggle();

        bool gripPressed = GetGripPressed();
        bool keyboardFallback = (handNode == XRNode.LeftHand) ? Input.GetKey(KeyCode.Q) : Input.GetKey(KeyCode.E);
        bool wantGrab = gripPressed || keyboardFallback;

        if (wantGrab && _grabbedRb == null)
        {
            TryGrab();
        }
        else if (!wantGrab && _grabbedRb != null)
        {
            Release();
        }

        if (_grabbedRb != null)
        {
            SubmitThisHandDelta(_grabbedRb);
        }
    }

    private void LateUpdate()
    {
        ApplyAllFrameDeltasOncePerFrame();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & grabbableLayers.value) == 0)
            return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        _hoverRb = rb;
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && rb == _hoverRb)
            _hoverRb = null;
    }

    private void TryGrab()
    {
        if (_hoverRb == null) return;

        _grabbedRb = _hoverRb;
        _hoverRb = null;

        // Increment shared grab count so physics only re-enables when BOTH hands release
        if (!_grabCounts.ContainsKey(_grabbedRb)) _grabCounts[_grabbedRb] = 0;
        _grabCounts[_grabbedRb]++;

        // Only zero velocities if the body is NOT already kinematic
        if (!_grabbedRb.isKinematic)
        {
            _grabbedRb.linearVelocity = Vector3.zero;
            _grabbedRb.angularVelocity = Vector3.zero;
        }

        _grabbedRb.isKinematic = true;


        // Reset pose history so first delta is clean
        _prevHandPos = transform.position;
        _prevHandRot = transform.rotation;
        _hasPrevPose = true;
    }

    private void Release()
    {
        if (_grabbedRb == null) return;

        if (_grabCounts.ContainsKey(_grabbedRb))
        {
            _grabCounts[_grabbedRb]--;
            if (_grabCounts[_grabbedRb] <= 0)
            {
                _grabCounts.Remove(_grabbedRb);
                _grabbedRb.isKinematic = false;
            }
        }
        else
        {
            _grabbedRb.isKinematic = false;
        }

        _grabbedRb = null;
        _hasPrevPose = false;
    }

    private void SubmitThisHandDelta(Rigidbody rb)
    {
        Vector3 currPos = transform.position;
        Quaternion currRot = transform.rotation;

        if (!_hasPrevPose)
        {
            _prevHandPos = currPos;
            _prevHandRot = currRot;
            _hasPrevPose = true;
            return;
        }

        Vector3 deltaPos = currPos - _prevHandPos;
        Quaternion deltaRot = currRot * Quaternion.Inverse(_prevHandRot);

        _prevHandPos = currPos;
        _prevHandRot = currRot;

        SubmitDelta(rb, handNode, currPos, deltaPos, deltaRot);
    }

    private static void SubmitDelta(Rigidbody rb, XRNode hand, Vector3 pivotPos, Vector3 deltaPos, Quaternion deltaRot)
    {
        if (!_frameStates.TryGetValue(rb, out FrameState state))
        {
            state = new FrameState();
            _frameStates[rb] = state;
        }

        // New frame: capture baseline object pose ONCE (before any deltas applied)
        if (state.frame != Time.frameCount)
        {
            state.frame = Time.frameCount;
            state.objPos0 = rb.position;
            state.objRot0 = rb.rotation;

            state.left = new HandDelta { active = false };
            state.right = new HandDelta { active = false };
        }

        HandDelta hd = new HandDelta
        {
            active = true,
            pivotPos = pivotPos,
            deltaPos = deltaPos,
            deltaRot = deltaRot
        };

        if (hand == XRNode.LeftHand) state.left = hd;
        else if (hand == XRNode.RightHand) state.right = hd;
        else
        {
            // If something odd happens, treat as "right" by default
            state.right = hd;
        }
    }

    private static void ApplyAllFrameDeltasOncePerFrame()
    {
        if (_lastAppliedFrame == Time.frameCount) return;
        _lastAppliedFrame = Time.frameCount;

        // Apply only states updated this frame
        foreach (var kvp in _frameStates)
        {
            Rigidbody rb = kvp.Key;
            FrameState st = kvp.Value;

            if (st.frame != Time.frameCount) continue;

            bool leftActive = st.left.active;
            bool rightActive = st.right.active;
            if (!leftActive && !rightActive) continue;

            Vector3 pos0 = st.objPos0;
            Quaternion rot0 = st.objRot0;

            // Translation: add both hands' delta positions
            Vector3 deltaPosTotal = Vector3.zero;
            if (leftActive) deltaPosTotal += st.left.deltaPos;
            if (rightActive) deltaPosTotal += st.right.deltaPos;

            // Rotation: multiply quaternions (order matters; keep deterministic)
            Quaternion deltaRotL = leftActive ? st.left.deltaRot : Quaternion.identity;
            Quaternion deltaRotR = rightActive ? st.right.deltaRot : Quaternion.identity;
            Quaternion deltaRotTotal = deltaRotR * deltaRotL; // Right * Left

            if (_doubleRotationEnabled)
                deltaRotTotal = deltaRotTotal * deltaRotTotal; // doubles angle

            // Rotate-around-controller translation term: sum of (r2 - r) from each hand, using baseline pos0
            Vector3 pivotContribution = Vector3.zero;

            if (leftActive)
            {
                Vector3 r = pos0 - st.left.pivotPos;
                Vector3 r2 = deltaRotL * r;
                pivotContribution += (r2 - r);
            }

            if (rightActive)
            {
                Vector3 r = pos0 - st.right.pivotPos;
                Vector3 r2 = deltaRotR * r;
                pivotContribution += (r2 - r);
            }

            Vector3 newPos = pos0 + deltaPosTotal + pivotContribution;
            Quaternion newRot = deltaRotTotal * rot0;

            rb.position = newPos;
            rb.rotation = newRot;
        }

        // Optional cleanup: remove frame states for objects no longer grabbed
        // (keeps dictionary from growing if you grab many objects)
        var toRemove = new List<Rigidbody>();
        foreach (var rb in _frameStates.Keys)
        {
            if (!_grabCounts.ContainsKey(rb))
                toRemove.Add(rb);
        }
        foreach (var rb in toRemove)
            _frameStates.Remove(rb);
    }

    private bool GetGripPressed()
    {
        if (!_device.isValid) return false;
        if (_device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip))
            return grip;
        return false;
    }

    private void HandleDoubleRotationToggle()
    {
        if (!allowDoubleRotationToggle) return;

        // Keyboard toggle (backup)
        if (Input.GetKeyDown(KeyCode.T))
            _doubleRotationEnabled = !_doubleRotationEnabled;

        // Controller toggle (use right hand primary button to avoid double-toggling)
        if (handNode != XRNode.RightHand) return;

        if (_device.isValid && _device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pb))
        {
            if (pb && !_prevPrimaryButton)
                _doubleRotationEnabled = !_doubleRotationEnabled;

            _prevPrimaryButton = pb;
        }
    }
}
