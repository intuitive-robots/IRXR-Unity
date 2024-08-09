using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

public class SceneRayMovable : MonoBehaviour
{
    [SerializeField] float savingInterval = 1.0f;
    [SerializeField] GameObject axisIndicator;
    [SerializeField] GameObject rayInteraction;
    private bool isMovable = false;

    void Start()
    {
        DeactivateInteractable(); 
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One)) // button A locks / unlocks the scene
        {
            ToggleMovableLock();
        }
    }

    public void ToggleMovableLock()
    {
        if (isMovable)
        {
            DeactivateInteractable();
        }
        else
        {
            ActivateInteractable();
        }
    }

    public void ActivateInteractable()
    {
        axisIndicator.SetActive(true);
        rayInteraction.SetActive(true);
        isMovable = true;
    }

    public void DeactivateInteractable()
    {
        axisIndicator.SetActive(false);
        rayInteraction.SetActive(false);
        isMovable = false;
    }
   
}