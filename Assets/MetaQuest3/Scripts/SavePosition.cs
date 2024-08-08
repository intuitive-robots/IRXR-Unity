using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

public class SavePosition : MonoBehaviour
{
    [SerializeField] float savingInterval = 1.0f;
    [SerializeField] GameObject axisIndicator;
    private SphereCollider _collider;
    private ColliderSurface _surface;
    private RayInteractable _rayInteractable;
    private bool isMovable = false;
    private GameObject _sceneObj;
    private Grabbable _grabbable;

    void Start()
    {
        _collider = GetComponent<SphereCollider>();
        _surface = GetComponent<ColliderSurface>();
        _rayInteractable = GetComponent<RayInteractable>();
        _sceneObj = GetComponent<SceneLoader>().GetSimObject();
        // _grabbable = GetComponent<Grabbable>();
        DeactivateInteractable(); 
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One)) // button A locks / unlocks the scene
        {
            DeactivateInteractable();
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
        _collider.enabled = true;
        _surface.enabled = true;
        _rayInteractable.enabled = true;
        axisIndicator.SetActive(true);
        // _grabbable.enabled = true;
        isMovable = true;
    }

    public void DeactivateInteractable()
    {
        _collider.enabled = false;
        _surface.enabled = false;
        _rayInteractable.enabled = false;
        axisIndicator.SetActive(false);
        // _grabbable.enabled = false;
        isMovable = false;
    }


    public void ToggleSceneLock()
    {
        if (TryGetComponent<RayInteractable>(out var ri))
        {
            if (ri.MaxInteractors == 0) 
            {
                ri.MaxInteractors = -1;
                ri.MaxSelectingInteractors = -1;
            }
            else
            {
                ri.MaxInteractors = 0;
                ri.MaxSelectingInteractors = 0;
            }
        }
    }

   
}