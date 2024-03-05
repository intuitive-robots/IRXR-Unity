# Intuitive Robot & Mixed Reality (IRXR-Unity)

This repository aims to enhance robot simulation by making it more intuitive and interactive through Mixed Reality (MR). 
Current visualization devices, such as XR/AR/VR glasses, support only basic physics simulations and are not tailored for research-driven simulation due to their limited computational resources.

To ensure the reproducibility of simulation results, we have decoupled the simulation and rendering processes onto two separate machines. 
This separation allows for more efficient and accurate simulations. 
These two systems are interconnected via [SimPublisher](https://github.com/intuitive-robots/SimPublisher.git), which simplifies the process of remotely rendering simulated objects from the simulation machine.
This repo is the implementation of rendering part.

We have created some applications based on this framework, and supported applications could be found through [application](#applications).

## Core Modules

- [IRXRObject](https://github.com/intuitive-robots/IRXRObject.git)
- QRAnchor

## Applications

### Create human demonstration by Augmented Reality

[Docs](https://github.com/intuitive-robots/HDAR) | [Paper Website](https://intuitive-robots.github.io/HDAR-Simulator/)

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

The XR/AR/VR device we have tested is:
- HoloLens 2

## System Setting

### Run the server code from the PC

Choose [application](#applications) you want to use, please find the detail information from the application document.

We use [SimPublisher](https://github.com/intuitive-robots/SimPublisher.git) to make the communication eaiser. It is also highly recommanded to create your own application by using this repo.

### Deploy Unity Project

Deploy this unity project scene to your XR/AR/VR device by Unity, here is a example about [how to deploy a Unity project to Hololens2](https://learn.microsoft.com/en-us/windows/mixed-reality/develop/advanced-concepts/using-visual-studio?tabs=hl2).

### Setting up your XR/AR/VR device and run the application

Setting up and keep your XR/AR/VR device and your simulation PC in a subnet.

Run the server application and Unity application,
and this project will automatically search available PC in the subnet.