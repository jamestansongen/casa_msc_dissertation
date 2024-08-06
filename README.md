Part 1: Blender - Create Study Area
---
- Download Blender and install [Blosm](https://prochitecture.gumroad.com/l/blender-osm) plugin.<br/>
- Get a bounding box for your area of interest such as from [bbox tool](https://norbertrenner.de/osm/bbox.html). This project used 51.540, 51.556, -0.1385, -0.0963 as the coordinates which corresponds to a subset of Islington and Camden.<br/>
- Follow Blosm instructions to generate study area. Export objects as FBX to import in Unity.


**Part 2: Unity - Run Simulation**
---
The following are the list of prefabs/gameobjects and their settings.
- Cube: Ensure Tag is "RedBox", Layer is "RedBox", Box Collider is attached.
- Delivery Vehicle: Ensure Layer is "Truck", Mesh Collider and TruckDroneDeployment_Simulation (Script) is attached.
- Drone: Ensure Layer is "Drone", Box Collider, Rigidbody and DroneMovement_Simulation (Script) is attached.
- TruckManager: Gameobject with TruckManager_Simulation (Script) attached.
- SimulationManager: Gameobject with SimulationManager (Script) attached. Placed in Hierarcy. 
- map_3.osm_buildings: FBX object from Blender. Ensure Layer is "Building", Mesh Collider and DemandSpawn_Simulation (Script) is attached. Placed in Hierarcy.
- map_3.osm_roads_primary/ residential/ secondary/ service/ tertiary/ track/ trunk/ unclassified: FBX object from Blender. Ensure Layer is "Ground" and Mesh Collider is attached. Placed in Hierarchy.

The following are the list of submodels, their functions and how to configure it. Ignore scripts in the Basic Scripts and [Buggy] Energy Consumption folders.
- DemandSpawn_Simulation: This submodel spawns a number of delivery points (represented by Red Box Prefab) within a certain radius of buildings (but not inside buildings) and on the "ground layer", ttach this to the parent gameobject in the Hierarchy (e.g. OSM Buildings)//In the Inspector, set Red Box Prefab to the Cube Prefab, Spawn Radius as 100, "Building" for Building Layer Mask and "Ground" for Ground Layer Mask.
- TruckManager_Simulation: This submodel places trucks (represented by Delivery Vehicle Prefab) according to the "K-Means" (k-means algorithm to create clusters from the delivery points followed by placing trucks at centroids) or "Random" (randomly and approximately equally distribute delivery points followed by placing trucks at centroids) positioning method, while ensuring a minimum distance between trucks and trucks are placed on "Ground" layer.//In the Inspector: set Truck Prefab to the Delivery Vehicle Prefab, Search Radius to 500, Max Attempts to 10, Min Distance Between Truck to 15.
- TruckDroneDeployment_Simulation
- DroneMovement_Simulation
- SimulationManager


**Part 3: Python - Data Analysis**
---



