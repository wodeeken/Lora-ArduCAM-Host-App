# Lora-ArduCAM-Host-App

Host app for collecting images from a CubeCell HTCC-AB01 board configured with an ArduCAM OV2640 camera that transmits data to the receiver board, which then relays data to this host application via serial. Used in conjunction with the CubeCell-LoRa-ArduCam-Receiver(https://github.com/wodeeken/CubeCell-LoRa-ArduCam-Receiver) and CubeCell-LoRa-ArduCam-Transmitter(https://github.com/wodeeken/CubeCell-LoRa-ArduCam-Transmitter) projects.

# Notes
1. This program requires the .NET 8 SDK/Runtime to compile and run. 
2. Only Windows and Linux OS are currently supported.
3. This program requires the receiver board to receive Lora image data and relay it via USB to this host application.
