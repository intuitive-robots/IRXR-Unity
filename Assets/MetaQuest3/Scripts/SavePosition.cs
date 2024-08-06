using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SavePosition : MonoBehaviour
{
    private float timer = 0.0f;
    [SerializeField] float savingInterval = 1.0f;
    [SerializeField] GameObject axisIndicator;
    void Start()
    {
        LoadTransform();
        axisIndicator.SetActive(false);
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.One)) // button A locks / unlocks the scene
        {
            ToggleSceneLock();
        }

        timer += Time.deltaTime;
        if (timer > savingInterval)
        {
            SaveTransform();
            timer = 0.0f;
        }
        
    }

    public void ToggleSceneLock()
    {
        RayInteractable ri = GetComponent<RayInteractable>();
        if (ri != null)
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
        else
        {
            Debug.LogError("No ray interactable found in Scene children!");
        }
    }

    void SaveTransform()
    {
        // Save position
        PlayerPrefs.SetFloat("PositionX", transform.position.x);
        PlayerPrefs.SetFloat("PositionY", transform.position.y);
        PlayerPrefs.SetFloat("PositionZ", transform.position.z);       

        // Save rotation
        PlayerPrefs.SetFloat("RotationX", transform.rotation.x);
        PlayerPrefs.SetFloat("RotationY", transform.rotation.y);
        PlayerPrefs.SetFloat("RotationZ", transform.rotation.z);

        PlayerPrefs.Save();
    }

    void LoadTransform()
    {
        if (PlayerPrefs.HasKey("PositionX"))
        {
            // Load position
            float posX = PlayerPrefs.GetFloat("PositionX");
            float posY = PlayerPrefs.GetFloat("PositionY");
            float posZ = PlayerPrefs.GetFloat("PositionZ");
            transform.position = new Vector3(posX, posY, posZ);

            // Load rotation
            float rotX = PlayerPrefs.GetFloat("RotationX");
            float rotY = PlayerPrefs.GetFloat("RotationY");
            float rotZ = PlayerPrefs.GetFloat("RotationZ");
            transform.rotation = Quaternion.Euler(rotX, rotY, rotZ);

        }
        else
        {
            Debug.LogWarning("No transform data found in PlayerPrefs.");
        }
    }
}