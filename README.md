# 🌊 Under the Memory

## 📖 Overview

**Under the Memory** is an immersive VR experience that reconstructs Bristol Harbourside using **3D Gaussian Splatting**, combining photorealistic spatial capture with environmental storytelling and interactive exploration.

Set within a future affected by long-term sea-level rise, the experience allows players to investigate the remains of Bristol Harbourside, recover fragmented archival information, and revisit memories of the same locations through 360° recordings of the present-day environment.

Players progress through the experience using tools such as the **Archive Scanner**, **Memory Bubbles**, and **Archive Lens**, gradually uncovering how the environment, its inhabitants, and surrounding ecosystems changed over time.

The narrative develops from exploration and memory into environmental collapse and an attempted ecosystem rescue, before concluding with the discovery of a larger surviving ecological environment that introduces a more hopeful perspective on adaptation and recovery.

<br>

## ✨ Key Features

### 🌆 3D Gaussian Splatting Environment

- **Photorealistic Reconstruction:** Uses 3D Gaussian Splatting to recreate areas of Bristol Harbourside within VR
- **Place-Based Storytelling:** Real-world locations form the foundation of the environmental narrative
- **Future Transformation:** Familiar urban spaces are reinterpreted as part of a future environment affected by long-term sea-level rise

### 🔍 Archive Scanner

- **Environmental Scanning:** Players investigate buildings and points of interest within the submerged environment
- **Fragmented Archives:** Scanning reveals incomplete environmental and historical information
- **Progression System:** Collecting sufficient archival information allows the player to unlock further stages of the experience

### 🫧 Memory Bubbles

- **Location-Based Memories:** Players revisit earlier representations of the same places they are exploring
- **360° Real-World Footage:** Memory Bubbles use captured 360° recordings of the present-day environment
- **Past and Future Comparison:** Players can directly compare remembered locations with their future submerged state

### 🔭 Archive Lens

- **Temporal Exploration:** The Archive Lens allows players to investigate how locations and events changed over time
- **Narrative Discovery:** Players uncover scenes showing people's experiences and behaviours as environmental conditions worsen
- **Environmental Context:** The lens connects present-day memories with the events leading towards the future environment

### 🌉 Environmental Transformation

- **Dynamic Environmental Events:** Major changes such as structural collapse reinforce the progression of environmental damage
- **Narrative Progression:** Changes to the environment guide the player from investigation towards increasingly severe consequences
- **Interactive Storytelling:** Environmental events are integrated directly into the player's progression through the experience

### 🌿 Ecosystem Exploration & Rescue

- **Small Ecosystem:** Players investigate and attempt to support a damaged ecological environment
- **Interactive Rescue Tasks:** Player choices and interactions contribute to an attempted ecosystem recovery
- **Consequences of Environmental Damage:** The sequence demonstrates that some ecological damage may have progressed beyond complete restoration
- **Larger Ecosystem:** The final stage introduces a previously unexplored ecological environment where players can observe and interact with wildlife

### 🥽 Immersive VR Interaction

- **Physical Interaction:** Players manipulate objects and complete environmental tasks through VR interaction
- **Exploration Tools:** Different tools provide distinct ways of investigating the environment and its history
- **Environmental Guidance:** Progression is driven through interaction, observation, and discovery rather than traditional menu-based storytelling

<br>

## 🛠️ Technical Specifications

- **Game Engine:** Unity 6.0 (`6000.0.59f2`)
- **Programming Language:** C#
- **XR Standard:** OpenXR
- **Interaction Framework:** XR Interaction Toolkit
- **Target Headset:** Meta Quest
- **Tested Device:** Meta Quest 3
- **Core Visual Technology:** 3D Gaussian Splatting
- **Rendering Pipeline:** Universal Render Pipeline (URP)
- **Platform:** Android / Meta Quest
- **Spatial Capture & 3D Tools:** RealityScan and Autodesk Maya
- **Media:** 360° video used for location-based memory experiences
- **Core Systems:** Gaussian Splat rendering, Archive Scanner, Memory Bubbles, Archive Lens, environmental interactions, narrative progression, ecosystem interactions, and VR object manipulation
<br>

## 📋 Requirements

### Hardware Requirements

- **VR Headset:** Meta Quest headset
- **Tested Device:** Meta Quest 3
- **Controllers:** Meta Quest Touch controllers
- **Development PC:** A system capable of running Unity and developing VR applications
- **USB Connection:** Required when deploying the application directly from Unity to the headset
- **Storage:** Sufficient free storage for the project's 3D Gaussian Splatting, 3D models, audio, and 360° video assets

### Software Requirements

- **Unity Hub**
- **Unity Editor:** Unity 6.0 (`6000.0.59f2`)
- **Android Build Support** with SDK, NDK and OpenJDK
- **Git LFS** for downloading large project assets
- **OpenXR**
- **XR Interaction Toolkit**

<br>

## 🚀 Installation & Setup

### 1. Install Git LFS

This repository contains large 3D Gaussian Splatting, model, audio, and video assets managed through **Git LFS**.

Install and initialise Git LFS before cloning the repository:

```bash
git lfs install
```

### 2. Clone the Repository

```bash
git clone https://github.com/YOUR-USERNAME/Under-The-Memory_Final-Project.git
cd Under-The-Memory_Final-Project
```

Download the large assets managed through Git LFS:

```bash
git lfs pull
```

### 3. Open the Project

- Open **Unity Hub**
- Select **Add → Add project from disk**
- Choose the cloned project folder
- Open the project using **Unity 6.0 (`6000.0.59f2`)**
- Allow Unity to restore the required packages and regenerate the `Library` folder

### 4. Prepare the Meta Quest Headset

- Enable **Developer Mode** on the Meta Quest headset
- Connect the headset to the development PC using USB
- Allow **USB debugging** when prompted
- Ensure the headset is recognised by Unity
- Confirm that **OpenXR** is enabled for the project

### 5. Build & Run

- Open the required project scene in Unity
- Go to **File → Build Profiles**
- Select **Android**
- Switch to the Android platform if required
- Select the connected Meta Quest headset
- Build and run the application

> Due to the size of the 3D Gaussian Splatting and other environment assets, the initial clone, Git LFS download, and Unity import may take some time.

<br>

## 🥽 How to Experience

### 1. Onboarding & Scanner Recovery

The experience begins within the future underwater environment.

- Follow the introductory guidance provided within the scene
- Clear debris and environmental objects surrounding the buried scanner setup
- Complete the required interactions to recover and activate the **Archive Scanner**
- Become familiar with the main interaction mechanics before progressing further

### 2. Explore with the Archive Scanner

The **Archive Scanner** allows the player to investigate the submerged environment and recover fragmented archival information.

- Explore buildings and environmental points of interest
- Scan designated locations and objects
- Collect incomplete environmental and historical records
- Gradually build an understanding of what happened to the surrounding area

Collecting enough archival information allows the player to progress further into the experience.

### 3. Enter Memory Bubbles

**Memory Bubbles** allow the player to revisit memories associated with the location they are currently exploring.

- Enter an unlocked Memory Bubble
- Experience a **360° recording of the real-world location**
- Compare the present-day memory with its future submerged state
- Observe how familiar places have changed over time

The system creates a direct connection between the remembered Bristol Harbourside and the future environment surrounding the player.

### 4. Investigate with the Archive Lens

The **Archive Lens** provides another way to investigate the history of the environment.

- Examine environmental clues and selected locations through the lens
- Observe changes across different moments in time
- Discover narrative scenes showing people's experiences as environmental conditions gradually worsen
- Use these scenes to understand how the future environment developed

### 5. Witness Environmental Collapse

As the narrative progresses, the player begins to experience increasingly severe environmental consequences.

- Observe major environmental changes and structural collapse
- Progress through scenes representing the worsening effects of sea-level rise
- Connect previously discovered memories and archival information with the changing environment

### 6. Attempt to Rescue the Ecosystem

The player eventually reaches a damaged **small ecosystem** and is given the opportunity to intervene.

- Investigate the condition of the ecosystem
- Complete interactive rescue tasks
- Make decisions while attempting to support its recovery
- Experience the consequences of environmental damage that has progressed beyond complete restoration

The sequence demonstrates that intervention does not always guarantee recovery once ecological damage has reached a critical stage.

### 7. Discover the Larger Ecosystem

Following the failed rescue attempt, a new route becomes accessible.

- Travel through the newly opened tunnel system
- Enter a larger and previously unexplored ecological environment
- Explore the underground ecosystem
- Observe and interact with animals and environmental elements

The final stage shifts the experience from environmental loss towards a more hopeful perspective centred on ecological survival and adaptation.

<br>

## 🎯 Core Systems & Interactions

### 🌆 3D Gaussian Splatting System

3D Gaussian Splatting is used to reconstruct real areas of Bristol Harbourside and provide a photorealistic foundation for the experience.

- Represents captured real-world locations within VR
- Connects environmental storytelling directly to recognisable places
- Supports the contrast between remembered locations and their transformed future state

### 🔍 Archive Scanner System

The **Archive Scanner** allows players to investigate the submerged environment and uncover fragmented archival information.

- Scans designated buildings and environmental points
- Reveals fragmented records connected to the surrounding environment
- Supports progression through the exploration stages
- Must be recovered through an introductory debris-clearing interaction before use

### 🧹 Scanner Recovery Interaction

The scanner is initially buried beneath debris and must be recovered through a series of physical VR interactions.

- Players remove different types of debris surrounding the scanner
- Sediment is cleared through repeated hand-wiping movements
- Visual feedback communicates progress as debris is removed
- The scanner becomes accessible once the required clearance tasks have been completed

### 🫧 Memory Bubble System

**Memory Bubbles** connect the future environment with memories of the same real-world locations.

- Uses 360° recordings of present-day locations
- Allows players to experience a memory while positioned within its future counterpart
- Creates a direct visual comparison between remembered and transformed environments
- Unlocks as the player progresses through archival discovery

### 🔭 Archive Lens System

The **Archive Lens** allows players to investigate environmental change across different moments in time.

- Reveals additional information associated with environmental clues
- Presents narrative scenes connected to the progression of the disaster
- Helps players understand how people, places, and environmental conditions changed over time

### 🌉 Environmental Transformation System

Environmental events are used to communicate the increasing consequences of long-term sea-level rise.

- Structural and environmental changes occur as the narrative progresses
- Major events, including environmental collapse, alter the player's surroundings
- Transitions connect discovered memories and archival information with their future consequences

### 🌿 Ecosystem Interaction System

The later stages of the experience shift towards ecological exploration and intervention.

- Players investigate a damaged small ecosystem
- Interactive tasks allow the player to attempt to support its recovery
- The outcome demonstrates the limitations of intervention after severe environmental damage
- A larger ecosystem is later revealed, allowing further exploration and interaction with wildlife

### 🥽 VR Interaction System

The experience uses physical VR interaction rather than relying primarily on traditional menus.

- Object grabbing and manipulation
- Hand/controller-based environmental interaction
- Physical debris-clearing tasks
- Tool-based investigation using the Archive Scanner and Archive Lens
- Exploration-driven progression through the environment

<br>

## 📝 Credits

### Development Team

- Pranavv Jothinathan
- Suhang Liu
- Zheng Wang
- Yuchen Sun
- Danna Kwon

Developed as part of the MSc Immersive Technologies programme at the University of Bristol.

### Technologies & Tools

The project was developed using technologies and tools including:

- Unity
- C#
- OpenXR
- XR Interaction Toolkit
- 3D Gaussian Splatting
- RealityScan
- Autodesk Maya

### Third-Party Assets & Packages

The project incorporates third-party packages, 3D models, textures, audio, animations, and other assets where applicable.

All third-party content remains subject to the licences and terms of use provided by its respective creators and distributors.

<br>

## 🔮 Future Enhancements

Potential future improvements include:

- Further optimise 3D Gaussian Splatting rendering for standalone VR performance
- Expand the number of reconstructed Bristol locations and archival points
- Add additional Memory Bubbles with more location-based 360° memories
- Expand the ecosystem rescue interactions and ecological decision-making
- Improve onboarding and guidance for first-time VR users
