Overview
---
This repository is part of my MSc Urban Spatial Science Dissertation for AY23/24. The overall workflow involves generating a study area in Blender, running the agent-based model (ABM) in Unity and carry out the analysis in Python.

Part 1: Blender - Create Study Area
---
- Download Blender and install [Blosm](https://prochitecture.gumroad.com/l/blender-osm) plugin.<br/>
- Get a bounding box for your area of interest such as from [bbox tool](https://norbertrenner.de/osm/bbox.html). This project used 51.540, 51.556, -0.1385, -0.0963 as the coordinates which corresponds to a subset of Islington and Camden.<br/>
- Follow Blosm instructions to generate study area. Export objects as FBX to import in Unity.


**Part 2: Unity - Run Simulation**
---
The following are the list of prefabs/gameobjects (Assets folder) and their settings.
- Cube: Ensure Tag is "RedBox", Layer is "RedBox", Box Collider is attached.
- Delivery Vehicle: Ensure Layer is "Truck", Mesh Collider and TruckDroneDeployment_Simulation (Script) is attached.
- Drone: Ensure Layer is "Drone", Box Collider, Rigidbody and DroneMovement_Simulation (Script) is attached.
- TruckManager: Gameobject with TruckManager_Simulation (Script) attached.
- SimulationManager: Gameobject with SimulationManager (Script) attached. Placed in Hierarcy. 
- map_3.osm_buildings: FBX object from Blender. Ensure Layer is "Building", Mesh Collider and DemandSpawn_Simulation (Script) is attached. Placed in Hierarcy.
- map_3.osm_roads_primary/ residential/ secondary/ service/ tertiary/ track/ trunk/ unclassified: FBX object from Blender. Ensure Layer is "Ground" and Mesh Collider is attached. Placed in Hierarchy.

The following are the submodels (Assets folder), their functions and how to configure it. Ignore scripts in the Basic Scripts and [Buggy] Energy Consumption folders.
- DemandSpawn_Simulation: This submodel spawns a number of delivery points (represented by Red Box Prefab) within a certain radius of buildings (but not inside buildings) and on the "ground layer", ttach this to the parent gameobject in the Hierarchy (e.g. OSM Buildings) // In the Inspector, set Red Box Prefab as Cube Prefab, Spawn Radius as 100, "Building" for Building Layer Mask and "Ground" for Ground Layer Mask.
- TruckManager_Simulation: This submodel places trucks (represented by Delivery Vehicle Prefab) according to the "K-Means" (k-means algorithm to create clusters from the delivery points followed by placing trucks at centroids) or "Random" (randomly and approximately equally distribute delivery points followed by placing trucks at centroids) positioning method, while ensuring a minimum distance between trucks and trucks are placed on "Ground" layer. // In the Inspector, set Truck Prefab as Delivery Vehicle, Search Radius to 500 metres, Max Attempts to 10, Min Distance Between Truck to 15 metres.
- TruckDroneDeployment_Simulation: Sorts drones in the list according to furthest to nearest delivery points. // In the Inspector, set Drone Prefab as Drone Prefab and Deployment Delay to 15 seconds.
- DroneMovement_Simulation: Guides the drones movement from truck to delivery point and back while avoiding buildings and other drones using artificial potential fields. // In the Inspector, set Flight Height to 60 metres, Hover Height to 15 metres, Speed to 15 metres/second, Delivery Time to 10 seconds, Avoidance Radius to 30 metres, Drone Avoidance Strength to 30 Newtons, Building Avoidance Strength to 10 metres, Max Avoidance Force to 30 Newtons, Proximity Radius to 60 metres, "Drone" for Drone Layer Mask, "Building" for Building Layer Mask and 5 metres for Delivery Distance Threshold.
- SimulationManager: Responsible for running the simulation for different scenarios and number of runs before returning a CSV file.  // Edit the script for SimulationManager and set the list of boxCounts, truckCounts and numberofRuns. In the Inspector, set Demand Spawn Prefab as map_3.osm_buildings, Truck Manager Prefab as TruckManager gameobject and Max Simulation Time to 600 seconds.

Once the above are setup, press the play button to run the simulation. Upon clicking the button again or simulation complete, a CSV file with the results will be located in the Assets folder.


**Part 3: Python - Data Analysis**
---
- Python Notebook in Data Analysis folder to run an analysis of the simulation results. Change the link to the CSV file or output directory for images as needed.


