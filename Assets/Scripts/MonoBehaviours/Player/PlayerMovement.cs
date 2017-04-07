using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;

public class PlayerMovement : MonoBehaviour {
    public Animator animator;
    public NavMeshAgent agent;
    public float inputHoldDelay = 0.5f;
    public float turnSpeedThreshold = 0.5f;
    public float speedDumpTime = 0.1f;
    public float slowingSpeed = 0.1f;
    public float turnSmoothing = 15f;
    public Animation anime;

    public Camera camera;
    private Vector3 cameraDefaultPosition;
    private Quaternion cameraDefaultRotation;

    private WaitForSeconds inputHoldWait;
    private Vector3 destinationPosition;
    private Interactable currentInteractable;
    private bool handleInput = true;

    private const float navMeshSampleDistance = 4f;
    private const float stopDistanceProportion = 0.1f;
    private readonly int hashSpeedParam = Animator.StringToHash("Speed");
    private readonly int hashLocomotionTag = Animator.StringToHash("Locomotion");

    private void Start()
    {
        agent.updateRotation = false;
        inputHoldWait = new WaitForSeconds(inputHoldDelay);
        destinationPosition = transform.position;
        cameraDefaultPosition = camera.transform.position;
        cameraDefaultRotation = camera.transform.rotation;
    }

    private void OnAnimatorMove()
    {
        agent.velocity = animator.deltaPosition / Time.deltaTime;
    }

    private void Update()
    {
        if (agent.pathPending)
        {
            return;
        }

        float speed = agent.desiredVelocity.magnitude;

        if (agent.remainingDistance <= agent.stoppingDistance * stopDistanceProportion)
        {
            Stopping(out speed);
        }

        else if (agent.remainingDistance <= agent.stoppingDistance)
        {
            Slowing(out speed, agent.remainingDistance);
        }

        else if (speed > turnSpeedThreshold)
        {
            Moving();
        }
        animator.SetFloat(hashSpeedParam, speed, speedDumpTime, Time.deltaTime);
    }

    private void Stopping(out float speed)
    {
        agent.Stop();
        transform.position = destinationPosition;
        speed = .0f;
        if (currentInteractable)
        {
            transform.rotation = currentInteractable.interactionLocation.rotation;
            currentInteractable.Interact();
            setCameraPosition();
            currentInteractable = null;
            StartCoroutine(WaitForInteraction());
        }
    }

    private void resetCameraPosition()
    {
        camera.transform.position = cameraDefaultPosition;
        camera.transform.rotation = cameraDefaultRotation;
    }

    private void setCameraPosition()
    {
        camera.transform.rotation = currentInteractable.interactionLocation.rotation;
        camera.transform.position = new Vector3(currentInteractable.interactionLocation.position.x - 5, currentInteractable.interactionLocation.position.y + 3, currentInteractable.interactionLocation.position.z);
        
    }

    private void Slowing(out float speed, float distanceToDestination)
    {
        agent.Stop();
        transform.position = Vector3.MoveTowards(transform.position, destinationPosition, slowingSpeed * Time.deltaTime);
        float proportionalDistance = 1f - distanceToDestination / agent.stoppingDistance;
        speed = Mathf.Lerp(slowingSpeed, 0f, proportionalDistance);
        Quaternion targetRotation = currentInteractable ? currentInteractable.interactionLocation.rotation : transform.rotation;
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, proportionalDistance);
    }

    private void Moving()
    {
        Quaternion targetRotation = Quaternion.LookRotation(agent.desiredVelocity);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, turnSmoothing * Time.deltaTime);
    }

    public void OnGroundClick(BaseEventData data)
    {
        if (!handleInput)
        {
            return;
        }
        currentInteractable = null; // In Case We Changed Minds and Click on the Ground after Interactable
        PointerEventData pData = (PointerEventData)data;
        NavMeshHit hit;
        if (
            NavMesh.SamplePosition(
                pData.pointerCurrentRaycast.worldPosition, 
                out hit, 
                navMeshSampleDistance, 
                NavMesh.AllAreas
            )
        )
        {
            destinationPosition = hit.position;
        }
        else
        {
            destinationPosition = pData.pointerCurrentRaycast.worldPosition;
        }
        agent.SetDestination(destinationPosition);
        agent.Resume();
    }

    public void OnInteractableClick(Interactable interactable)
    {
        if (!handleInput)
        {
            return;
        }
        currentInteractable = interactable;
        destinationPosition = currentInteractable.interactionLocation.position;
        agent.SetDestination(destinationPosition);
        agent.Resume();
    }

    private IEnumerator WaitForInteraction()
    {
        handleInput = false;
        yield return inputHoldWait;
        while (animator.GetCurrentAnimatorStateInfo(0).tagHash != hashLocomotionTag)
        {
            yield return null;
        }
        handleInput = true;
        resetCameraPosition();
    }
}
