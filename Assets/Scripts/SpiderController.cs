﻿using UnityEngine;
using System.Collections;
using Raycasting;

[DefaultExecutionOrder(-1)] //Make sure the players input spider movement is applied before the spider itself will do a ground check and possibly add gravity
public class SpiderController : MonoBehaviour {

    public Spider spider;

    [Header("Camera")]
    public SmoothCamera smoothCam;

    private void FixedUpdate() {
        //** Movement **//
        Vector3 input = getInput();

        //Adds an acceleration/deceleration to smooth out the movement.
        spider.walk(input);

        Quaternion tempCamTargetRotation = smoothCam.getCamTargetRotation();
        Vector3 tempCamTargetPosition = smoothCam.getCamTargetPosition();
        spider.turn(input);
        smoothCam.setTargetRotation(tempCamTargetRotation);
        smoothCam.setTargetPosition(tempCamTargetPosition);
    }

    void Update() {
        // Since the spider might have adjusted its normal, rotate camera target halfway back here (More smooth experience instead of camera freezing in place with every normal adjustment)
        Vector3 n = spider.getLastNormal();
        if (n != Vector3.zero) {
            float angle = Vector3.SignedAngle(spider.getLastNormal(), spider.transform.up, smoothCam.getCameraTarget().right);
            smoothCam.RotateCameraVertical(0.5f * -angle);
        }
        //Hold down Space to deactivate ground checking. The spider will fall while space is hold.
        spider.setGroundcheck(!Input.GetKey(KeyCode.Space));
    }

    private Vector3 getInput() {
        Vector3 input = Vector3.ProjectOnPlane(smoothCam.transform.forward, spider.transform.up).normalized * Input.GetAxis("Vertical") + (Vector3.ProjectOnPlane(smoothCam.transform.right, spider.transform.up).normalized * Input.GetAxis("Horizontal"));
        Quaternion fromTo = spider.getLookRotation(spider.transform.right, spider.getGroundNormal()) * Quaternion.Inverse(spider.transform.rotation);
        input = fromTo * input;
        float magnitude = input.magnitude;
        return (magnitude <= 1) ? input : input /= magnitude;
    }
}