using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class XBotMovingScript : MonoBehaviour
{
    private Animator _animator;
    void Start()
    {
        // Getting current animator
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        float axisV = Input.GetAxis("Vertical");
        transform.position += Time.deltaTime * axisV * 5 * transform.forward;
        _animator.SetFloat("movingSpeed", axisV * 5);
    }
}
