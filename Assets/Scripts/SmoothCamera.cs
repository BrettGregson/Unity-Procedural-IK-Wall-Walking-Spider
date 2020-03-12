﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Raycasting;

[RequireComponent(typeof(Camera))]
public class SmoothCamera : MonoBehaviour {

    public bool showDebug;
    public Transform parent;

    private Transform camTarget;
    private Camera cam;

    [Header("Smoothness")]
    public float lerpSpeed;

    [Header("Sensitivity")]
    [Range(1, 5)]
    public float XSensitivity;
    [Range(1, 5)]
    public float YSensitivity;

    [Header("Angle Restrictions")]
    [Range(0.01f, 90.0f)]
    public float camUpperAngleMargin = 30.0f;
    [Range(0.01f, 90.0f)]
    public float camLowerAngleMargin = 60.0f;

    [Header("Camera Clipping")]
    [Range(0, 1f)]
    public float rayRadiusForObstructions;
    public LayerMask cameraInvisibleClipLayer;
    public LayerMask cameraClipLayer;

    [Range(0, 0.5f)]
    public float cameraClipMargin = 0.1f;

    private SphereCast camToPlayer;
    private RayCast playerToCam;
    float maxCameraDistance;
    private RaycastHit hitInfo;
    private RaycastHit[] camObstructions;
    private ShadowCastingMode[] camObstructionsCastingMode;

    void Awake() {

        cam = GetComponent<Camera>();
        setupCamTarget();

        // Now unparent the camera itself so it can move freely and use the above target to lerp
        transform.parent = null;

        initializeRayCasting();
    }

    private void setupCamTarget() {
        // For the target, create new Gameobject with same position and rotation as camera starts with and parent to Spider
        GameObject g = new GameObject("SmoothCamera Target");
        camTarget = g.transform;
        camTarget.position = transform.position;
        camTarget.rotation = transform.rotation;
        camTarget.transform.parent = parent;
    }

    private void initializeRayCasting() {
        maxCameraDistance = Vector3.Distance(parent.position, transform.position);
        playerToCam = new RayCast(parent.position, camTarget.position, parent, null);
        camToPlayer = new SphereCast(transform.position, parent.position, rayRadiusForObstructions * transform.lossyScale.y * 0.01f, transform, parent);
    }

    private void Update() {
        RotateCameraHorizontal(Input.GetAxis("Mouse X") * XSensitivity, false);
        RotateCameraVertical(-Input.GetAxis("Mouse Y") * YSensitivity, false);
        clipCamera();
        clipCameraInvisible();
    }

    private void LateUpdate() {

        Vector3 a = parent.InverseTransformPoint(transform.position);
        Vector3 b = parent.InverseTransformPoint(camTarget.position);

        Vector3 c = Vector3.Slerp(a, b, Time.deltaTime * lerpSpeed);
        transform.position = parent.TransformPoint(c);

        //transform.position = Vector3.SmoothDamp(transform.position, camTarget.position, ref velocity, lerpTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, camTarget.rotation, Time.deltaTime * lerpSpeed);

        if (showDebug) drawDebug();

    }

    public void RotateCameraHorizontal(float angle, bool onlyTarget = true) {
        if (angle == 0) return;
        camTarget.RotateAround(parent.position, parent.transform.up, angle);
        if (!onlyTarget) transform.RotateAround(parent.position, parent.transform.up, angle);
    }

    public void RotateCameraVertical(float angle, bool onlyTarget = true) {

        angle = angle % 360;
        if (angle == -180) angle = 180;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
        //Now angle is of the form (-180,180]

        if (angle == 0) return;
        float currentAngle = Vector3.SignedAngle(parent.transform.up, parent.transform.position - camTarget.position, camTarget.right); //Should always be positive
        if (currentAngle + angle < camLowerAngleMargin) {
            angle = camLowerAngleMargin - currentAngle;
        }
        if (currentAngle + angle > 180.0f - camUpperAngleMargin) {
            angle = 180.0f - camUpperAngleMargin - currentAngle;
        }
        camTarget.RotateAround(parent.position, camTarget.right, angle);
        if (!onlyTarget) transform.RotateAround(parent.position, camTarget.right, angle);
    }

    void clipCamera() {
        playerToCam.setEnd(camTarget.position);
        playerToCam.setDistance(maxCameraDistance);

        if (playerToCam.castRay(out hitInfo, cameraClipLayer)) {
            float margin = Mathf.Min(cameraClipMargin, 0.9f * Vector3.Distance(hitInfo.point, parent.position));
            setTargetPosition(hitInfo.point - margin * playerToCam.getDirection());
        }
        else setTargetPosition(playerToCam.getEnd());

    }

    void clipCameraInvisible() {

        //First make all previous obstructions visible again.
        if (camObstructions != null) {
            for (int k = 0; k < camObstructions.Length; k++) {
                MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
                if (mesh == null) continue;
                mesh.shadowCastingMode = camObstructionsCastingMode[k];
            }
        }

        // Now transparent all new obstructions
        camObstructions = camToPlayer.castRayAll(cameraInvisibleClipLayer);
        camObstructionsCastingMode = new ShadowCastingMode[camObstructions.Length];
        for (int k = 0; k < camObstructions.Length; k++) {
            MeshRenderer mesh = camObstructions[k].transform.GetComponent<MeshRenderer>();
            if (mesh == null) continue;
            camObstructionsCastingMode[k] = mesh.shadowCastingMode;
            mesh.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    public Camera getCamera() {
        return cam;
    }

    public Transform getCameraTarget() {
        return camTarget;
    }

    public Vector3 getCamTargetPosition() {
        return camTarget.position;
    }

    public Quaternion getCamTargetRotation() {
        return camTarget.rotation;
    }

    public Transform getTargetParent() {
        return parent;
    }

    public void setTargetPosition(Vector3 pos) {
        camTarget.position = pos;
    }

    public void setTargetRotation(Quaternion rot) {
        camTarget.rotation = rot;
    }

    //** Debug Methods **//
    private void drawDebug() {
        camToPlayer.draw(Color.white);
        DebugShapes.DrawRay(camToPlayer.getOrigin(), cameraClipMargin * camToPlayer.getDirection(), Color.black);

        float currentAngle = Vector3.SignedAngle(parent.transform.up, parent.transform.position - camTarget.position, camTarget.right);
        Vector3 v = Quaternion.AngleAxis(-currentAngle, camTarget.right) * camToPlayer.getDirection();
        Vector3 up = parent.TransformDirection(Quaternion.AngleAxis(camUpperAngleMargin, camTarget.right) * v);
        Vector3 down = parent.TransformDirection(Quaternion.AngleAxis(camLowerAngleMargin, camTarget.right) * v);
        DebugShapes.DrawRay(parent.transform.position, up, Color.black);
        DebugShapes.DrawRay(parent.transform.position, down, Color.black);

        DebugShapes.DrawPoint(camTarget.position, Color.magenta, 0.1f);
        DebugShapes.DrawRay(camTarget.position, camTarget.forward, Color.blue);
        DebugShapes.DrawRay(camTarget.position, camTarget.right, Color.red);
        DebugShapes.DrawRay(camTarget.position, camTarget.up, Color.green);


    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected() {
        if (UnityEditor.EditorApplication.isPlaying) return;
        if (!UnityEditor.Selection.Contains(transform.gameObject)) return;
        if (parent == null) return;
        camTarget = transform;
        initializeRayCasting();
        drawDebug();
    }
#endif
}