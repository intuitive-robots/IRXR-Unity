using System;

using UnityEngine;

namespace IRXR
{
    public class TaskRecorderManager : IRXRSingleton<TaskRecorderManager>
    {
        public Action OnStartRecord;
        public Action OnStopRecord;
        public Action OnResetScene;
        public Action OnNewTaskReady;
        public Action OnTaskFinished;
        public bool OnRecording { get; private set; }


        private void Start() {
            OnNewTaskReady += () => { OnRecording = false; };
            OnStartRecord += () => { OnRecording = true; };
            OnStopRecord += () => { OnRecording = false; };
        }

        public void StartRecord(){
            IRXRClient.Instance.SendRequest("start_record", "");
            OnStartRecord?.Invoke();
        }

        public void StopRecord(){
            IRXRClient.Instance.SendRequest("stop_record", "");
            OnStopRecord?.Invoke();
        }

        public void SaveReset(){
            IRXRClient.Instance.SendRequest("save_and_reset", "");
            Debug.Log("Save and Reset");
            if (OnRecording)
            {
                OnStopRecord?.Invoke();
            }
            OnResetScene?.Invoke();
        }

        public void ResetScene(){
            IRXRClient.Instance.SendRequest("reset", "");
            if (OnRecording)
            {
                OnStopRecord?.Invoke();
            }
            OnResetScene?.Invoke();
        }

    }
}