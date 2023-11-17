using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    private float distance = 10f;
    public Vector3 offset;

    Transform focus;
    Vector3 originalDelta;

    public void Calibrate(Transform focus)
    {
        this.focus = focus;
        originalDelta = transform.position - focus.position;
        originalDelta.x *= .01f;
    }

    private void LateUpdate()
    {
        if (focus == null) return;

        Vector3 t = focus.position+offset*.5f + (originalDelta.normalized * (distance));
        transform.position = Vector3.Lerp(transform.position,  t, Time.deltaTime*2.5f);

        Quaternion r = Quaternion.LookRotation((focus.position+offset - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, r, Time.deltaTime * 1f);
    }
}
