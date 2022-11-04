using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class XBotMovingScript : MonoBehaviour
{
    [SerializeField] private float maximumForwardSpeed;
    [SerializeField] private float maximumBackwardSpeed;
    [SerializeField] private float mouseSensitivity;
    [SerializeField] private bool invertY;
    
    private Animator _animator;
    private Transform _rootTransform;
    void Start()
    {
        // Getting current animator
        _animator = GetComponent<Animator>();
        // Getting XBotRoot transform
        _rootTransform = transform.parent.GetComponent<Transform>();
    }

    void Update()
    {
        float axisVertical = Input.GetAxis("Vertical");
        float axisHorizontal = Input.GetAxis("Horizontal");
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        _rootTransform.position += Time.deltaTime * axisVertical * 5 * _rootTransform.forward;
        _animator.SetFloat("movingSpeed", axisVertical * 5);
    }
}
