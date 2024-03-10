# Intuitive Robot & Mixed Reality (IRXR-Unity)

This repository aims to enhance robot simulation by making it more intuitive and interactive through Mixed Reality (MR). 
Current visualization devices, such as XR/AR/VR glasses, support only basic physics simulations and are not tailored for research-driven simulation due to their limited computational resources.

To ensure the reproducibility of simulation results, we have decoupled the simulation and rendering processes onto two separate machines. 
This separation allows for more efficient and accurate simulations. 
These two systems are interconnected via [SimPublisher](https://github.com/intuitive-robots/SimPublisher.git), which simplifies the process of remotely rendering simulated objects from the simulation machine.
This repo is the implementation of rendering part.

We have created some submodules based on this framework, and all of they could be found through [submodules](#submodules).

Currently, the following applications is under maintained.
- HDAR: Create human demonstration by Augmented Reality
- IRXIObject: Generate 3D unity models from server
- QRAnchor: Use QR code to align the virtual objects to real world
- 6D Pose Estimation Adjustment
- Object Joint Indicator

## Software Dependency

For the basic application:
- [MRTK2](https://learn.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/mrtk2/?view=mrtkunity-2022-05)
The configuration of MRTK relied on [Mixed Reality Feature Tool](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/unity/welcome-to-mr-feature-tool)
- [Newtonsoft.Json-for-Unity](https://github.com/applejag/Newtonsoft.Json-for-Unity)
- [NativeWebSocket](https://github.com/endel/NativeWebSocket)

For QRAnchor:
- [Microsoft.MixedReality.QR](https://www.nuget.org/packages/Microsoft.MixedReality.QR)

## Hardware Requirement

You need one XR/AR/VR device and one PC for running simulation,
the ip addresses of these two device should be in the same subnet.

The XR/AR/VR device we have tested:
- HoloLens 2

## System Setting

### Run the server code from the PC

We use [SimPublisher](https://github.com/intuitive-robots/SimPublisher.git) to make the communication eaiser. 
It is also highly recommanded to create your own application by using this repo.
For each submodule, the coresponding server project could be found in the README.md from the submodules repo.

## Set up the Unity project

Clone this repo by

```bash
git clone https://github.com/intuitive-robots/IRXR-Unity.git
```

Choose [submodule](#submodules) you want to use and clone it

```bash
git clone --recurse-submodules <submodule_url>
```

Open this project by Unity Editor,
choose one scene from Asset/[submodule_repo]/Scene.
Click the start button to start this application in the play mode.

### Deploy the Unity project

Deploy this unity project scene to your XR/AR/VR device by Unity, here is a example about [how to deploy a Unity project to Hololens2](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/advanced-concepts/using-visual-studio?tabs=hl2).

### Setting up your XR/AR/VR device and run the application

Keep your XR/AR/VR device and your simulation PC in a subnet.

Run the server and this Unity application from the XR/AR/VR device,
and this Unity project will automatically search available PC in the subnet and connect.

## Submodules

### HDAR: Create human demonstration by Augmented Reality

[Submodule Code](https://github.com/intuitive-robots/HDAR) | [Paper Website](https://intuitive-robots.github.io/HDAR-Simulator/)

The [HDAR](https://github.com/intuitive-robots/HDAR) applicatin is used to create human demonstration by Augmented Reality.

This application supports our paper ["A Comprehensive User Study on Augmented Reality-Based Data Collection Interfaces for Robot Learning"](https://intuitive-robots.github.io/HDAR-Simulator/), which was published on HRI2024. If you find our work useful, please consider citing.

```latex
@inproceedings{jiang2024comprehensive,
  title={A Comprehensive User Study on Augmented Reality-Based Data Collection Interfaces for Robot Learning},
  author={Jiang, Xinkai and Mattes, Paul and Jia, Xiaogang and Schreiber, Nicolas and Neumann, Gerhard and Lioutikov, Rudolf},
  booktitle={Proceedings of the 2024 ACM/IEEE International Conference on Human-Robot Interaction},
  pages={10},
  year={2024},
  organization={ACM},
  address={Boulder, CO, USA},
  date={March 11--14}
}
```