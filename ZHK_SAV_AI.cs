// #define ZHK_Debug

using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ZHK_SAV_AI : UdonSharpBehaviour
{
    /*
    /   Zhakami Modules - SAV AI
    /   D: ZhakamiZhako#2147 | TW: @ZZhako | gmail: zhintamizhakami@gmail.com
    /   v 0.0003 - 
    /   For use in development and testing purposes. Not for redistribution outside Sacchill's Jet boys and/or selected persons
    /
    /       The current state of this shit is only to test how the planes behave during taxi, sound testing, flying as a dummy target, follow waypoints
    /  and to validate an aircraft's behaviour on certain things. This is not ready for world distribution quality; but rather this is meant only to 
    /  validate certain things such as damage handling, sound checking, target checking, taxiing and other things.
    /
    /       The use of this script is at your own risk.
    /
    /   Instructions:
    /       - Apply a ZHK_SAV_AI to a SaccAirVehicle by adding it as a separate gameobject component. Assign SaccAirVehicle, Waypoints and other behaviours. Stock values
    /           are recommended. Assign as an Udon Extension Behaviour.
    /       - You are expected to place DFUNC_ToggleEngine as well in the aircraft.
    /       - Insert a ZHK_AI_BRAKE object and assign it for both the ZHK_SAV_AI and the SaccEntity's Udon Extension Behaviours. Assign the DFUNC_Brake object of it. This will change on the next update.
    /
    /   Waypoint guides
            Assigning waypoints will have to be done on transforms. You may have multiple attributes on each waypoint. The waypoint attributes shall be named on the gameobject and separated with the semicolon ";".
        Apart from this, the AI will ignore other strings and will treat it as a name. Assigning a waypoint without attributes will allow the aircraft to just continue towards the next waypoint without chaging whatever
        the last attributes were.

        e.g. SP=150;LD;WAYPOINT_BRAVO  
        - This means that the AI will take a speed in 150 knots, Landing mode. Anything else will be ignored.
    /
    /   Waypoint assignment
    /   Type    ||      Description
    /   TK      ||      Take off Cue. Will result to full throttle/Afterburner.
    /   LD      ||      Landing cue. Gears down.
    /   SP=xx   ||      Speed restriction. Where xx = speed in knots
    /   ESC     ||      Escort. Must be placed in the same hierarchy level of a SaccAirVehicle.
    /   
    /   How do i activate the AI?
    /   - Tick the 'IsActiveAI' checkbox.
    /
    /
    /   Combat?
    /   - Insert a ZHK_AI_TargetList.
    /
    /   Formation flights?
    /   - wip; Insert an empty GameObject with a name ESC; in it. 
    */


    public Transform[] Waypoints;

    // public GameObject[] Targets;

    [FieldChangeCallback(nameof(isActiveAI))] [UdonSynced(UdonSyncMode.None)]
    public bool _isActiveAI = false;

    public bool isActiveAI
    {
        set
        {
            _isActiveAI = value;
            //reserved
            // if (value)
            // {
            //     SAV.ThrottleOverridden = 1;
            //     SAV.JoystickOverridden = 1;
            // }
            // else
            // {
            //     SAV.ThrottleOverridden = 0;
            //     SAV.JoystickOverridden = 0;
            // }
        }
        get => _isActiveAI;
    }

    public float distanceToChangeWaypoint = 100f; //meters
    public float distanceToChangeWaypointTaxi = 50;

    public SaccAirVehicle SAV;
    private UdonBehaviour SAVU;
    public float ThrottleInput;
    public float RollInput;
    public float PitchInput;
    public float YawInput;
    public float BrakeInput;
    private float prevBrakeInput = 0f;
    public bool checkForward = true;
    public int checkState = 0; // 0 - bottom, 1 - forward, 2 - left, 3 - right, 4 - up
    public bool ExecuteEventsOnStart;
    public string[] runEventsOnStart;
    public float[] timePerEvent;
    public float timerEvent;
    public int EventIndex = 0;
    private bool ranEventsOnStart = false;

    [UdonSynced] public int _currentWaypointIndex = 0;

    public int currentWaypointIndex
    {
        get => _currentWaypointIndex;
        set
        {
            _currentWaypointIndex = value;
            UpdateWaypoint();
        }
    }

    public bool returnToAnotherWaypointWhenFinished = false;
    public int waypointReturnIndex = -1;
    public int LandingWaypointIndex = 0; // Reserved in the future
    public float currentSpeed;
    public float currentAltitude;

    public float TimeLerper = 4;

    [Header("Should be auto assigned")] public SaccEntity ENTITY;

    public Transform AircraftTransform;

    public SAV_EffectsController EFFECTS;
    public DFUNC_Brake DFUNC_BRAKE;
    public ZHK_AI_Brakes BRAKES;
    public DFUNC_Flaps DFUNC_FLAPS;
    public DFUNC_Limits DFUNC_LIMITS;
    public DFUNC_ToggleEngine DFUNC_ENGINETOGGLE;
    public DFUNC_Canopy DFUNC_CANOPY;
    public DFUNC_Bomb DFUNC_BOMB;
    public DFUNC_Flares DFUNC_FLARES;
    public DFUNC_Gear DFUNC_GEAR;
    public DFUNC_Cruise DFUNC_CRUISE;
    public DFUNC_Hook DFUNC_HOOK;
    public DFUNC_Gun DFUNC_GUN;
    public DFUNC_AAM DFUNC_AAM;
    public DFUNC_Smoke DFUNC_SMOKE;
    public ZHK_OpenWorldMovementLogic OpenWorldMovementLogic;

    // Reserved for Dialogue System
    // public UdonBehaviour DialogueManager;
    // public UdonBehaviour[] SpecialNumberTrigger;
    // public UdonBehaviour[] TriggerIncoming;
    // public UdonBehaviour[] TriggerTakeOff;
    // public UdonBehaviour[] TriggerApproach;
    // public UdonBehaviour[] TriggerTaxi;
    // public UdonBehaviour[] TriggerLanding;
    // public UdonBehaviour[] TriggerNavigate;
    // public UdonBehaviour[] TriggerAttacking;
    // public UdonBehaviour[] TriggerWeaponsRelease;
    // public UdonBehaviour[] TriggerOutOfAmmo;
    // public UdonBehaviour[] TriggerGuns;
    // public UdonBehaviour[] TriggerTakingDamage;
    // public UdonBehaviour[] TriggerEject;
    // public UdonBehaviour[] TriggerLowFuel;
    // public UdonBehaviour[] TriggerFox;
    // public UdonBehaviour[] TriggerClear;
    // public UdonBehaviour[] TriggerDead;
    // public UdonBehaviour[] TriggerEngineStarting;
    // public UdonBehaviour[] TriggerTaxiHalt;
    // public UdonBehaviour[] TriggerTaxiContinue;
    // public UdonBehaviour[] TriggerEngineOn;
    // public UdonBehaviour[] TriggerNumbers;

    public float taxiCollisionRadius = 40f;
    public float waitTime = 0f;

    public float waitTimer = 0f;
    public float TaxiWaitTime = 0f;

    public float TaxiWaitTimer = 0f;
    //new vars

    public float MinimumAnglePitch = 100f;
    public float MinimumAngleYaw = 100f;
    public float MinimumAngleRoll = 800f;

    public float YawAngleMax = 25;

    public float currentWaypointSpeed = 0f;
    public bool returnToOrigin = true;


    public float debug_PitchAngle = 0f;
    public float debug_YawAngle = 0f;
    public float debug_pitchAngleDot;
    public float debug_yawAngleDot;
    public float debug_deltaAngle;
    public Vector3 DebugTargetRoll = Vector3.zero;
    public Vector3 DebugRotationAxis = Vector3.zero;
    public float DebugDeltaAngle = 0f;

    public Vector3 ControlInputs;
    
    public float shouldRollAngle = 0f;
    public float maxRollAngle = 90f;
    public float rollStrength = 1000f;

    public float gunparticleForward = 30f;
    public Vector3 originalGunparticleForward = Vector3.zero;


    [Header("AI States")] public bool state_engineon = false;
    public bool state_canopy = false;

    [UdonSynced()] [FieldChangeCallback(nameof(state_combat))]
    public bool _state_combat = false;

    public bool state_combat
    {
        get => _state_combat;
        set
        {
            _state_combat = value;
            if (value)
            {
                if (DFUNC_AAM)
                {
                    DFUNC_AAM.gameObject.SetActive(true);
                }

                if (disableLimitsOnCombat)
                {
                    if (DFUNC_LIMITS)
                    {
                        DFUNC_LIMITS.gameObject.SetActive(false);
                    }
                }


            }

            if (!value)
            {
                foundAAM = false;
                air_combat_target_aamTarget = null;
                // air_combat_target = null;
                if (DFUNC_AAM)
                {
                    DFUNC_AAM.AAMTarget = 0;
                }

                if (disableLimitsOnCombat)
                {
                    if (DFUNC_LIMITS)
                    {
                        DFUNC_LIMITS.gameObject.SetActive(true);
                    }
                }
            }
        }
    }

    public bool state_refueling = false;
    public bool state_airrefueling = false; //reserved
    public bool state_lowfuel = false; //reserved
    public bool state_smoke = false;
    public bool state_flapsUp = false;
    public bool state_gearsUp = false;
    public bool state_landing = false;
    public bool state_takingoff = false;
    public bool state_escort;
    public bool state_taxiing;
    // AI Will deploy flaps when pulling up to add 'emergency' lift.
    [FieldChangeCallback(nameof(state_pullingup))] public bool _state_pullingup;

    public bool state_pullingup
    {
        get => _state_pullingup;
        set
        {
            _state_pullingup = value;
            if (value)
            {
                if (DFUNC_FLAPS && !DFUNC_FLAPS.Flaps)
                {
                    DFUNC_FLAPS.SetFlapsOn();
                }
            }
            else
            {
                if (DFUNC_FLAPS && DFUNC_FLAPS.Flaps)
                {
                    DFUNC_FLAPS.SetFlapsOff();
                }
            }
        }
    }
    public bool state_targeted = false; //reserved
    // AI will launch flares when state_missile is true. 
    [FieldChangeCallback(nameof(state_missile))]public bool _state_missile = false;

    public bool state_missile
    {
        set
        {
            _state_missile = value;
            if (value)
            {
                directionEvade = Random.Range(0, 4);
            }
            else
            {
                directionEvade = -1;
            }
        }
        get => _state_missile;
    }

    public int directionEvade = -1;

    [Tooltip("Startup time before the AI Script activates")][InspectorName("Startup Time")]public float initTime = 10f;
    private float initTimer = 0f;
    private bool inited = false;
    public bool useAsPilot = true;
    [HideInInspector]public bool setOnce;

    [Header("Taxi Options")] public float taxiClearanceDistance = 20;
    public LayerMask TaxiClearanceDetection;
    private float taxiCorrectionTime = 2f; //When OWML Kicks in, Planes may be 'afloat' while on the ground.
    private float taxiCorrectionTimer = 0f;

    [Header("Formation Settings")] public SaccAirVehicle EscortedAircraft;
    public float formationThreshold = 1.2f;
    public float minFormationThreshold = 0.8f;
    public float maxFormationThreshold = 2f;
    public float formationThresholdTimeMultiplier = 1f;


    [Header("Combat Settings")] 
    public float distanceToTarget = 0f;
    public bool disableLimitsOnCombat = false;
    public ZHK_SAV_AI_TargetList targetList;
    public float rangeForEngagement = 80000;
    public SaccAirVehicle air_combat_target;
    public GameObject air_combat_target_aamTarget;
    public bool canDogfight = false;
    public float engageRadius = 12000;
    public LayerMask AAMTargetLayer;
    public float SpherecastTime = 5f;
    public float SpherecastTimer = 0f;
    public float GunRange = 3000f;
    public float minGunAngle = 5f;
    public float targetAngle = 0f;
    public float targetAngleMissile = 0f;
    public Transform gunOffset;
    public float combatSpeedFar = 800;
    public float combatSpeedNear = 400;

    public float minMissileRange = 4000f;
    public float maxMissileRange = 10000f;
    public float missileCooldownTime = 20f;
    private float missileCooldownTimer = 0f;
    public float MaxAngleMissile = 5f;
    private bool foundAAM = false;


    private bool initCruiseTaxi = false;
    private float CruiseIntegrator;
    private float DeltaTime;
    private float CruiseIntegratorMin = -5;
    private float CruiseIntegratorMax = 5;
    private float CruiseProportional = .1f;
    private float CruiseIntegral = .1f;
    private float TriggerTapTime = 1;

    private float FlareTimer = 0f;
    public float FlareTime = 1f;

    private KeyCode AfterburnerKey = KeyCode.T;
    private RaycastHit hit;
    private RaycastHit targetChecker;
    private RaycastHit terrainCheck;

    private float raycastTerrainTime = .4f;
    private float raycastTerrainTimer = 0f;
    public float minimumPullupDist = 3000f;
    public LayerMask TerrainLayers;
    public float Limit = 5f;
    private bool breakScan = false;
    private int scanIndex = 0;
    public RaycastHit[] SpherecastStuff = new RaycastHit[10];

    // brake stuff

    // public AudioSource Airbrake_snd;
    // public Animator BrakeAnimator;
    // public Transform GroundBrakeForcePosition;
    // public float BrakeInput;
    // public bool HasAirBrake;
    // public float AirBrakeStrength;
    // public float GroundBrakeStrength;
    // public float WaterBrakeStrength;
    // public bool NoPilotAlwaysGroundBrake;
    // public float GroundBrakeSpeed;
    //
    //
    
    public void SFEXT_O_LowFuel()
    {
        state_lowfuel = true;
    }

    public void SFEXT_O_NoFuel()
    {
        //reserved
    }

    public void SetActiveAI()
    {
        isActiveAI = true;
        RequestSerialization();
    }

    public void SetInactiveAI()
    {
        isActiveAI = false;
        RequestSerialization();
    }

    public void SFEXT_O_RespawnButton()
    {
        if (SAV.Occupied)
        {
            SAV.Occupied = false;
            SAV.SFEXT_O_RespawnButton();
            BrakeInput = 0;
            RollInput = 0;
            YawInput = 0;

            SFEXT_G_ReAppear();
        }
    }

    public void SFEXT_L_AAMTargeted()
    {
        // reserved for dialogue 
    }


    public void SFEXT_L_EntityStart()
    {
        startAI = isActiveAI;
        ENTITY = SAV.EntityControl;
        SAVU = SAV.gameObject.GetComponent<UdonBehaviour>();
        EFFECTS = (SAV_EffectsController) ENTITY.GetExtention(GetUdonTypeName<SAV_EffectsController>());

        DFUNC_BRAKE = (DFUNC_Brake) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Brake>());
        BRAKES = (ZHK_AI_Brakes) ENTITY.GetExtention(GetUdonTypeName<ZHK_AI_Brakes>());

        DFUNC_FLAPS = (DFUNC_Flaps) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Flaps>());
        DFUNC_LIMITS = (DFUNC_Limits) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Limits>());
        DFUNC_ENGINETOGGLE = (DFUNC_ToggleEngine) ENTITY.GetExtention(GetUdonTypeName<DFUNC_ToggleEngine>());
        DFUNC_CANOPY = (DFUNC_Canopy) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Canopy>());
        DFUNC_BOMB = (DFUNC_Bomb) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Bomb>());
        DFUNC_FLARES = (DFUNC_Flares) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Flares>());
        DFUNC_GEAR = (DFUNC_Gear) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Gear>());
        DFUNC_HOOK = (DFUNC_Hook) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Hook>());
        DFUNC_GUN = (DFUNC_Gun) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Gun>());
        DFUNC_AAM = (DFUNC_AAM) ENTITY.GetExtention(GetUdonTypeName<DFUNC_AAM>());
        DFUNC_CRUISE = (DFUNC_Cruise) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Cruise>());
        DFUNC_SMOKE = (DFUNC_Smoke) ENTITY.GetExtention(GetUdonTypeName<DFUNC_Smoke>());
        if (DFUNC_GUN)
        {
            originalGunparticleForward = DFUNC_GUN.GunDamageParticle.localPosition;
            DFUNC_GUN.GunDamageParticle.position = new Vector3(originalGunparticleForward.x,
                originalGunparticleForward.y, originalGunparticleForward.z + gunparticleForward);
        }

        OpenWorldMovementLogic =
            (ZHK_OpenWorldMovementLogic) ENTITY.GetExtention(GetUdonTypeName<ZHK_OpenWorldMovementLogic>());
        if (DFUNC_CRUISE)
        {
            initCruiseTaxi = DFUNC_CRUISE.AllowCruiseGrounded;
        }

        AfterburnerKey = SAV.AfterBurnerKey;
        SAV.AfterBurnerKey = KeyCode.None;

        if (BRAKES)
        {
            if (DFUNC_BRAKE)
            {
                DFUNC_BRAKE.gameObject.SetActive(false);
                DFUNC_BRAKE.enabled = false;
            }
        }

        AircraftTransform = SAV.EntityControl.transform;

        gameObject.SetActive(true);

        if (OpenWorldMovementLogic != null)
        {
            OpenWorldMovementLogic.AI = true;
        }
    }

    public void SFEXT_G_Dead()
    {
        state_engineon = false;
        state_canopy = false;
        state_combat = false;
        state_refueling = false;
        state_lowfuel = false;
        state_airrefueling = false;
        state_gearsUp = false;
        state_flapsUp = false;
        state_escort = false;
        EscortedAircraft = null;

        if (DFUNC_GUN) DFUNC_GUN.Firing = false;

        currentWaypointIndex = 0;
    }

    public void ClearCombat()
    {
        state_combat = false;
        if (air_combat_target)
        {
            air_combat_target.EntityControl.SendEventToExtensions("ZHKEXT_T_RWRClear_net");
        }

        air_combat_target = null;
        currentWaypointSpeed = 0;
        ThrottleInput = .75f;
    }

    public void SFEXT_G_ReAppear()
    {
        state_engineon = false;
        state_canopy = false;
        state_combat = false;
        state_refueling = false;
        state_lowfuel = false;
        state_airrefueling = false;
        state_gearsUp = false;
        state_flapsUp = false;
        EscortedAircraft = null;
        SAV.Occupied = false;
        BrakeInput = 0;
        RollInput = 0;
        YawInput = 0;

        currentWaypointIndex = 0;
        inited = false;
        initTime = 5f;
        initTimer = 0f;

        timerEvent = 0;
        EventIndex = 0;
        ranEventsOnStart = false;
    }

    public bool startAI = false;

    public void Update()
    {
        if (Networking.IsOwner(gameObject))
        {
            if (!inited && initTimer < initTime)
            {
                initTimer = initTimer + Time.deltaTime;
                return;
            } 
            if (!inited && initTimer > initTime)
            {
                if (ExecuteEventsOnStart && !ranEventsOnStart)
                {
                    if (EventIndex < runEventsOnStart.Length)
                    {
                        if (timerEvent > timePerEvent[EventIndex])
                        {
                            ENTITY.SendEventToExtensions(runEventsOnStart[EventIndex]);
                            EventIndex = EventIndex + 1;
                            timerEvent = 0;
                        }
                        else
                        {
                            timerEvent = timerEvent + Time.deltaTime;
                        }
                    }
                    else
                    {
                        ranEventsOnStart = true;
                    }
                }
                else
                {
                    inited = true;
                    isActiveAI = true;
                    RequestSerialization();   
                }
            }

            bool changeWaypoint = false;
            currentSpeed = SAV.CurrentVel.magnitude * 1.9438445f;
            // currentAltitude = OpenWorldMovementLogic ? OpenWorldMovementLogic.Map.transform.position +  : AircraftTransform.position.y * 3.28084f;

            //Owner only.
            if (isActiveAI)
            {
                if (!setOnce)
                {
                    // Debug.Log("Overriding Controls...");
                    if (SAV.JoystickOverridden < 1)
                    {
#if ZHK_Debug
                        Debug.Log("Overriding Joystick");
#endif
                        SAVU.SetProgramVariable("JoystickOverridden",
                            (int) SAVU.GetProgramVariable("JoystickOverridden") + 1);
                        // SAV.JoystickOverridden = 1;
                    }

                    if (SAV.ThrottleOverridden < 1)
                    {
                        Debug.Log("Overriding Throttle");
                        SAVU.SetProgramVariable("JoystickOverridden",
                            (int) SAVU.GetProgramVariable("ThrottleOverridden") + 1);
                        // SAV.ThrottleOverridden = 1;
                    }

                    setOnce = true;
                    SAV.SFEXT_L_KeepAwake();

                    if (BRAKES)
                    {
                        BRAKES.EnableAI = true;
                    }

                    if (DFUNC_CRUISE)
                    {
                        DFUNC_CRUISE.AllowCruiseGrounded = true;
                    }

                    ENTITY.SendEventToExtensions("ZHK_AI_ENABLE");

                    // if (runEventsOnStart.Length > 0)
                    // {
                    //     foreach (string s in runEventsOnStart)
                    //     {
                    //         ENTITY.SendEventToExtensions(s);
                    //     }
                    // }
                }


                if (!SAV.EngineOn && SAV.Fuel > 0f)
                {
                    if (!state_engineon)
                    {
                        EFFECTS.gameObject.GetComponent<UdonBehaviour>().SendCustomNetworkEvent(NetworkEventTarget.All,
                            nameof(EFFECTS.SFEXT_G_PilotEnter));
                        state_engineon = true;
                        BrakeInput = 0;
                        if (DFUNC_ENGINETOGGLE)
                        {
                            if (!ExecuteEventsOnStart)
                            {  
                                DFUNC_ENGINETOGGLE.KeyboardInput();
                                waitTime = DFUNC_ENGINETOGGLE.StartUpTime;
                            }
                            else
                            {
                                float total = 0;
                                foreach (float x in timePerEvent)
                                {
                                    total = total + x;
                                }
                                waitTime = total;
                            }
                            
                        }
                    }

                    if (state_engineon)
                    {
                        if (state_taxiing)
                        {
                            if (waitTimer < waitTime)
                            {
                                EFFECTS.DoEffects = 0f;
                                if (waitTimer / waitTime > 0 && waitTimer / waitTime < 0.3f)
                                {
                                    PitchInput = Mathf.Lerp(PitchInput, 1f, Time.deltaTime);
                                }

                                if (waitTimer / waitTime > 0.3f && waitTimer / waitTime < 0.4f)
                                {
                                    PitchInput = Mathf.Lerp(PitchInput, -1f, Time.deltaTime);
                                }

                                if (waitTimer / waitTime > 0.4f && waitTimer / waitTime < 0.5f)
                                {
                                    PitchInput = Mathf.Lerp(PitchInput, 0f, Time.deltaTime);
                                    RollInput = Mathf.Lerp(RollInput, 1f, Time.deltaTime);
                                }

                                if (waitTimer / waitTime > 0.5f && waitTimer / waitTime < 0.6f)
                                {
                                    PitchInput = 0f;
                                    RollInput = Mathf.Lerp(RollInput, -1f, Time.deltaTime);
                                }

                                if (waitTimer / waitTime > 0.6f && waitTimer / waitTime < 0.7f)
                                {
                                    RollInput = Mathf.Lerp(RollInput, 0f, Time.deltaTime);
                                    YawInput = Mathf.Lerp(YawInput, 1f, Time.deltaTime);
                                }

                                if (waitTimer / waitTime > 0.7f && waitTimer / waitTime < 0.8f)
                                {
                                    RollInput = 0f;
                                    YawInput = Mathf.Lerp(YawInput, -1f, Time.deltaTime);
                                }

                                if (waitTimer / waitTime > 0.8f && waitTimer / waitTime < 0.9f)
                                {
                                    YawInput = Mathf.Lerp(YawInput, 0f, Time.deltaTime);
                                }

                                if (waitTimer / waitTime > 0.9f)
                                {
                                    // Sequence done, setting controls to 0, wait timer to 0;
                                    YawInput = 0f;
                                    waitTime = 0f;
                                    waitTimer = 0f;
                                }
                            }
                            // add Controls check sequence here
                        }
                    }
                }

                if (SAV.EngineOn)
                {
                    if (!state_canopy)
                    {
                        state_canopy = true;

                        if (DFUNC_CANOPY)
                        {
                            DFUNC_CANOPY.KeyboardInput();
                        }

                        if (DFUNC_AAM)
                        {
                            DFUNC_AAM.gameObject.SetActive(true);
                        }

                        if (DFUNC_FLARES)
                        {
                            DFUNC_FLARES.gameObject.SetActive(true);
                        }

                        SAV.Occupied = true;
                        SAV.Asleep = false;
                        SAVU.SetProgramVariable("DisablePhysicsAndInputs", 0);
                        SAV.SFEXT_L_KeepAwake();
                        if (useAsPilot) SAV.SFEXT_O_PilotEnter();
                        SAV.EntityControl.SendEventToExtensions("SFEXT_G_PilotEnter");
                        UpdateWaypoint();
                        SAV.SetCollidersLayer(SAV.OutsideVehicleLayer); // set them back to solid ffs.
                        // SAV.Piloting = false;
                        // SAV.DisablePhysicsAndInputs = 0;
                    }
                }

                // State Checker
                if (SAV.Taxiing)
                {
                    state_taxiing = true;
                }
                else
                {
                    if (state_taxiing && taxiCorrectionTimer < taxiCorrectionTime)
                    {
                        taxiCorrectionTimer = taxiCorrectionTimer + Time.deltaTime;
                    }
                    else
                    {
                        state_taxiing = false;
                        taxiCorrectionTimer = 0f;
                    }
                }

                if (!state_taxiing)
                {
                    if (SAV.MissilesIncomingHeat > 0 || SAV.MissilesIncomingOther > 0 || SAV.MissilesIncomingRadar > 0)
                    {
                        state_missile = true;
                    }
                    else
                    {
                        if (state_missile)
                        {
                            state_missile = false;
                        }
                    }

                    if (state_missile)
                    {
                        Debug.Log("AI SHOULD BE FLARING");
                        if (DFUNC_FLARES)
                        {
                            if (FlareTimer > FlareTime)
                            {
                                DFUNC_FLARES.KeyboardInput();
                                FlareTimer = 0f;
                            }
                            else
                            {
                                FlareTimer = FlareTimer + Time.deltaTime;
                            }
                        }
                    }

                    if (Vector3.Distance(AircraftTransform.position, Waypoints[currentWaypointIndex].position) <
                        distanceToChangeWaypoint)
                    {
                        if (!state_escort)
                        {
                            changeWaypoint = true;
                        }
                        else
                        {
                            if (currentWaypointIndex + 1 < Waypoints.Length)
                            {
                                if (Vector3.Distance(AircraftTransform.position,
                                        Waypoints[currentWaypointIndex + 1].position) <
                                    distanceToChangeWaypoint)
                                {
                                    currentWaypointIndex = currentWaypointIndex + 1;
                                    changeWaypoint = true;
                                }
                            }
                            //else, do nothing.
                        }
                    }

                    if (!state_combat)
                    {
                        if (SAV.EngineOn)
                        {
                            moveLogic(Waypoints[currentWaypointIndex], Waypoints[currentWaypointIndex].transform.up);
                            if (!state_gearsUp && !state_landing)
                            {
                                if (DFUNC_GEAR && !DFUNC_GEAR.GearUp && !Physics.Raycast(SAV.GroundDetector.position,
                                    -SAV.GroundDetector.up, out hit, 8,
                                    SAV.GroundDetectorLayers,
                                    QueryTriggerInteraction.Ignore)) // Gearup only when you're 20m above. 
                                {
                                    DFUNC_GEAR.ToggleGear();
                                    state_gearsUp = true;
                                }
                            }

                            if (!state_landing && !state_flapsUp)
                            {
                                if (DFUNC_FLAPS && DFUNC_FLAPS.Flaps)
                                {
                                    DFUNC_FLAPS.ToggleFlaps();
                                    state_flapsUp = true;
                                }
                            }

                            if (!state_taxiing && !state_takingoff && canDogfight)
                            {
                                processDetectTargets();
                            }

                            if (state_landing)
                            {
                                
                            }
                        }
                    }
                    else if (state_combat)
                    {
                        if (combatSpeedFar == 0)
                        {
                            if (ThrottleInput < 1f) ThrottleInput = 1;
                            if (ThrottleInput == 1 && SAV.HasAfterburner && !SAV.AfterburnerOn) SAV.SetAfterburnerOn();
                        }

                        if (combatSpeedNear == 0)
                        {
                            if (ThrottleInput < 1f) ThrottleInput = 1;
                            if (ThrottleInput == 1 && SAV.HasAfterburner && !SAV.AfterburnerOn) SAV.SetAfterburnerOn();
                        }

                        if (combatSpeedFar > 0)
                        {
                            if (currentWaypointSpeed != combatSpeedFar)
                                currentWaypointSpeed = combatSpeedFar;

                            processCruise();
                        }

                        // 
                        // if (ThrottleInput == 1 && SAV.HasAfterburner && !SAV.AfterburnerOn) SAV.SetAfterburnerOn();
                        // if (air_combat_target == null) state_combat = false;
                        moveLogic(air_combat_target.transform, air_combat_target.transform.up);
                        Vector3 TargetAimingVectors = FirstOrderIntercept(AircraftTransform.position,
                            SAV.VehicleRigidbody.velocity,
                            DFUNC_GUN ? DFUNC_GUN.BulletSpeed : SAV.VehicleRigidbody.velocity.magnitude,
                            air_combat_target.transform.position, air_combat_target.AirVel);

                        distanceToTarget = Vector3.Distance(AircraftTransform.position,
                            air_combat_target.transform.position);

                        // Do something about combatspeed near
                        if (air_combat_target!=null && air_combat_target.dead)
                        {
                            ClearCombat();

                        }

                        if (air_combat_target!=null && air_combat_target.Health < 0)
                        {
                            ClearCombat();

                        }

                        if (distanceToTarget > rangeForEngagement) // disengage;
                        {
                            ClearCombat();
                        }
                        else
                        {
                            if(air_combat_target!=null){
                                Vector3 directionToTarget =
                                    TargetAimingVectors - (gunOffset ? gunOffset.position : AircraftTransform.position);
                                Vector3 directionToTargetMissile =
                                    air_combat_target.transform.position - AircraftTransform.position;
                                targetAngle = Vector3.Angle(gunOffset ? gunOffset.forward : AircraftTransform.forward,
                                    directionToTarget);
                                targetAngleMissile = Vector3.Angle(AircraftTransform.forward, directionToTargetMissile);
                                // Vector3 crossProduct = Vector3.Cross(AircraftTransform.forward, directionToTarget);
                                // if (crossProduct.y < 0)
                                // {
                                //     targetAngle = -targetAngle; // Target is on the left side
                                // }
                                if (distanceToTarget > minMissileRange && distanceToTarget < maxMissileRange)
                                {
                                    if (targetAngleMissile < MaxAngleMissile)
                                    {

                                        if (DFUNC_AAM && !DFUNC_AAM.sendtargeted)
                                        {
                                            DFUNC_AAM.sendtargeted = true;
                                            air_combat_target.EntityControl.SendEventToExtensions(
                                                "ZHKEXT_T_RWRWarning_net");
                                            // Place the sacc warning here. Idk it sucks atm.
                                        }

                                        missileCooldownTimer = missileCooldownTimer + Time.deltaTime;
                                        if (missileCooldownTimer > missileCooldownTime)
                                        {
                                            missileCooldownTimer = 0f;
                                            if (DFUNC_AAM)
                                            {
                                                // DFUNC_AAM.AAMFire++;
                                                DFUNC_AAM.RequestSerialization();
                                                DFUNC_AAM.SendCustomNetworkEvent(NetworkEventTarget.All,
                                                    nameof(DFUNC_AAM.LaunchAAM));
                                            }
                                        }
                                    }
                                    else
                                    {

                                        if (DFUNC_AAM && DFUNC_AAM.sendtargeted)
                                        {
                                            DFUNC_AAM.sendtargeted = false;
                                            air_combat_target.EntityControl.SendEventToExtensions(
                                                "ZHKEXT_T_RWRClear_net");
                                            // Place the sacc warning clear here. 
                                        }
                                    }
                                }

                                if (distanceToTarget < GunRange && targetAngle < minGunAngle)
                                {
                                    if (DFUNC_GUN && !DFUNC_GUN.Firing)
                                    {
                                        DFUNC_GUN.Firing = true;
                                        DFUNC_GUN.GunDamageParticle.gameObject.SetActive(true);
                                        DFUNC_GUN.RequestSerialization();
                                    }
                                }
                                else
                                {
                                    if (DFUNC_GUN && DFUNC_GUN.Firing)
                                    {
                                        DFUNC_GUN.Firing = false;
                                        DFUNC_GUN.GunDamageParticle.gameObject.SetActive(false);
                                        DFUNC_GUN.RequestSerialization();
                                    }
                                }
                            }
                        }
                    }
                }
                else if (state_taxiing)
                {
                    if (SAV.EngineOn)
                    {
                        if (TaxiWaitTime > 0)
                        {
                            if (currentSpeed > 2f)
                            {
                                if (BRAKES)
                                {
                                    BrakeInput = 1f;
                                    ThrottleInput = 0f;
                                    YawInput = 0f;
                                    RollInput = 0f;
                                }
                            }
                            else
                            {
                                if (BRAKES && BRAKES.BrakeInput > .5f)
                                {
                                    BrakeInput = 0f;
                                }
                            }

                            if (TaxiWaitTimer < TaxiWaitTime)
                            {
                                TaxiWaitTimer = TaxiWaitTimer + Time.deltaTime;
                            }
                            else
                            {
                                TaxiWaitTime = -1f;
                            }
                        }
                        else
                        {
                            processCruise();
                            if (state_takingoff && ThrottleInput < 1f) // resume
                            {
                                ThrottleInput = 1;
                            }

                            //Taxiing
                            moveLogic(Waypoints[currentWaypointIndex], Waypoints[currentWaypointIndex].transform.up);
                        }

                        if (Vector3.Distance(AircraftTransform.position, Waypoints[currentWaypointIndex].position) <
                            distanceToChangeWaypointTaxi)
                        {
                            changeWaypoint = true;
                        }

                        RaycastHit hit;

                        Physics.SphereCast(AircraftTransform.position, taxiCollisionRadius, AircraftTransform.forward,
                            out hit,
                            taxiClearanceDistance, TaxiClearanceDetection, QueryTriggerInteraction.Collide);

                        // Gizmos.DrawLine(AircraftTransform.position, hit.collider.transform.position);
                        Debug.DrawLine(AircraftTransform.position,
                            hit.collider != null ? hit.collider.transform.position : AircraftTransform.position,
                            Color.red);
                        if (hit.collider != null)
                        {
                            TaxiWaitTimer = 0;
                            TaxiWaitTime = 5f;
                        }
                    }
                }

                if (changeWaypoint)
                {
                    if (currentWaypointIndex + 1 < Waypoints.Length)
                    {
                        currentWaypointIndex = currentWaypointIndex + 1;
                    }
                    else if (returnToAnotherWaypointWhenFinished)
                    {
                        currentWaypointIndex = waypointReturnIndex;
                    }
                    else
                    {
                        currentWaypointIndex = 0;
                    }

                    RequestSerialization();
                }

                if (waitTimer > waitTime)
                {
                    //Do something about the timer
                }
                else
                {
                    waitTimer = waitTimer + Time.deltaTime;
                }

                ControlInputs.x = PitchInput;
                ControlInputs.y = YawInput;
                ControlInputs.z = RollInput;

                if (state_escort && EscortedAircraft)
                {
                    var dist = Vector3.Distance(SAV.VehicleRigidbody.transform.position,
                        Waypoints[currentWaypointIndex].transform.position);
                    if (dist / (currentSpeed / 1.944f) < maxFormationThreshold)
                    {
                        // Debug.Log("In Range of escort. to target speed.");
                        formationThreshold = dist / (currentSpeed / 1.944f);
                        currentWaypointSpeed = EscortedAircraft.AirSpeed;
                        processCruise();
                        if (dist / (currentSpeed / 1.944f) < minFormationThreshold)
                        {
                            BrakeInput = Mathf.Clamp(1 + (dist / (currentSpeed / 1.944f)), 0, 1);
                        }
                        else
                        {
                            BrakeInput = 0f;
                        }
                    }
                    else
                    {
                        // Debug.Log("Escort Too far. Full throttle. ");
                        currentWaypointSpeed = 0f;
                        ThrottleInput = 1;
                        formationThreshold = 0;
                        BrakeInput = 0f;
                    }
                }

                if (currentWaypointSpeed > 0 && TaxiWaitTime < 1)
                {
                    processCruise();
                }

                if (prevBrakeInput != BrakeInput)
                {
                    BRAKES.AIBrakeInput = BrakeInput;
                    prevBrakeInput = BrakeInput;
                }

                SAV.JoystickOverride = ControlInputs;
                SAV.ThrottleOverride = ThrottleInput;
                SAV.ThrottleInput = ThrottleInput;
                SAV.PlayerThrottle = ThrottleInput;
            }
            else
            {
                if (SAV.JoystickOverridden == 1)
                {
                    SAV.gameObject.GetComponent<UdonSharpBehaviour>().SetProgramVariable("JoystickOverridden", 0);
                }

                if (SAV.ThrottleOverridden == 1)
                {
                    SAV.gameObject.GetComponent<UdonSharpBehaviour>().SetProgramVariable("ThrottleOverridden", 0);
                }

                if (DFUNC_CRUISE)
                {
                    DFUNC_CRUISE.AllowCruiseGrounded = initCruiseTaxi;
                }

                if (BRAKES)
                {
                    DFUNC_BRAKE.enabled = true;
                    BRAKES.EnableAI = false;
                }

                SAV.AfterBurnerKey = AfterburnerKey; // return to original

                setOnce = false;
            }
        }
    }

    public void processDetectTargets()
    {
        if (breakScan)
        {
            if (SpherecastTimer > SpherecastTime)
            {
                Physics.SphereCastNonAlloc(AircraftTransform.position, engageRadius, AircraftTransform.up,
                    SpherecastStuff, 5f, AAMTargetLayer);
            }
            else
            {
                SpherecastTimer = SpherecastTimer + Time.deltaTime;
            }

            breakScan = false;
        }
        else
        {
            if (scanIndex >= SpherecastStuff.Length)
            {
                scanIndex = 0;
                breakScan = true;
            }
            else
            {
                if (SpherecastStuff[scanIndex].collider != null)
                {
                    int xb = 0;
                    foreach (RaycastHit x in SpherecastStuff)
                    {
                        if (x.collider != null)
                        {
                            xb = +1;
                        }
                    }

                    SaccAirVehicle test = SpherecastStuff[scanIndex].collider.transform.parent
                        .GetComponent<SaccAirVehicle>();
                    if (test != null)
                    {
                        if (targetList)
                        {
                            foreach (SaccAirVehicle x in targetList.TargetList)
                            {
                                if (x!=null && test == x && !test.Taxiing && test.Health > 0)
                                {
                                    if (test.Occupied)
                                    {
                                        state_combat = true;
                                        air_combat_target = test;
                                        air_combat_target_aamTarget = SpherecastStuff[scanIndex].collider.gameObject;
                                        if (DFUNC_AAM)
                                        {
                                            int dfuncAAMTarget = 0;
                                            foreach (GameObject xx in DFUNC_AAM.AAMTargets)
                                            {
                                                if (xx == air_combat_target_aamTarget)
                                                {
                                                    DFUNC_AAM.AAMTarget = dfuncAAMTarget;
                                                    DFUNC_AAM.RequestSerialization();
                                                    foundAAM = true;
                                                    break;
                                                }

                                                dfuncAAMTarget = dfuncAAMTarget + 1;
                                            }
                                        }   
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                scanIndex = scanIndex + 1;
            }
        }
    }

    public void CallSmoke()
    {
        state_smoke = !state_smoke;
        if (DFUNC_SMOKE != null)
        {
            if (state_smoke)
            {
                DFUNC_SMOKE.SmokeOn = true;
            }
            else
            {
                DFUNC_SMOKE.SmokeOn = false;
            }
        }
    }

    public void CallFlare()
    {
        // state_smoke = !state_smoke;
        if (DFUNC_FLARES != null)
        {
            DFUNC_FLARES.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(DFUNC_FLARES.KeyboardInput));
        }
    }

    public void processCruise()
    {
        DeltaTime = Time.deltaTime;
        float error = ((currentWaypointSpeed / 1.9438445f) - SAV.AirSpeed);
        CruiseIntegrator += error * DeltaTime;
        CruiseIntegrator = Mathf.Clamp(CruiseIntegrator, CruiseIntegratorMin, CruiseIntegratorMax);
        ThrottleInput = Mathf.Clamp((CruiseProportional * error) + (CruiseIntegral * CruiseIntegrator), 0, 1);
    }

    public void UpdateWaypoint()
    {
        string[] b = Waypoints[currentWaypointIndex].name.Split(';');

        // foreach (var x in b)
        // {
        //     Debug.Log(b);
        // }

        //Syntax
        //SP - Speed
        //TK - Takeoff
        //LD - Landing
        //ESC - escort
        if (b != null && b.Length > 0)
        {
            //NAME=VALUE
            bool foundSpeed = false;
            bool foundTakingoff = false;
            bool foundStateLanding = false;

            for (int x = 0; x < b.Length; x++)
            {
                string[] a = b[x].Split('=');
                foreach (var xx in a)
                {
                    Debug.Log("VAL:" + xx);
                }

                switch (a[0])
                {
                    case "ESC":
                        Debug.Log("Escort order");
                        SaccEntity target = Waypoints[currentWaypointIndex].parent.GetComponent<SaccEntity>();
                        UdonSharpBehaviour targetSAV = target.GetExtention(GetUdonTypeName<SaccAirVehicle>());

                        if (targetSAV != null)
                        {
                            SaccAirVehicle SAVEscort = targetSAV.gameObject.GetComponent<SaccAirVehicle>();
                            Debug.Log("Escort:", SAVEscort);
                            EscortedAircraft = SAVEscort;
                            state_escort = true;
                        }

                        break;
                    case "SP":
                        Debug.Log("Setting Cruise Speed to " + a[1]);
                        currentWaypointSpeed = float.Parse(a[1]);
                        foundSpeed = true;
                        break;
                    case "TK":
                        Debug.Log("State to Take off");
                        state_takingoff = true;
                        foundTakingoff = true;
                        break;
                    case "LD":
                        state_landing = true;
                        Debug.Log("State to landing");
                        foundStateLanding = true;
                        break;
                }
            }


            if (foundSpeed)
            {
                Debug.Log("Setting Cruise speed");
            }
            else
            {
                currentWaypointSpeed = -1f;
            }

            if (foundTakingoff)
            {
                ThrottleInput = 1;
                if (SAV.HasAfterburner) SAV.SetAfterburnerOn();
            }
            else
            {
                state_takingoff = false;
                if (ThrottleInput == 1)
                {
                    if (SAV.HasAfterburner) SAV.SetAfterburnerOff();
                }
            }

            if (foundStateLanding)
            {
                state_landing = true;
                if (DFUNC_GEAR)
                {
                    DFUNC_GEAR.SetGearDown();
                }
            }
        }
    }

    public void DoBrakes()
    {
    }

    public void moveLogic(Transform TargetPos, Vector3 up)
    {
        Vector3 targetVectors = TargetPos.position;
        if (state_missile)
        {
            Vector3 dir;
            switch (directionEvade)
            {
                case 0: dir = TargetPos.position; // ignore missile, proceed to target
                    break;
                case 1:dir = -AircraftTransform.right * 100; //evade to the left 
                    break;
                case 2:dir = AircraftTransform.right * 100; //evade to the right
                    break;
                case 3:dir = Vector3.up * 100; //evade to up
                    break;
                case 4:
                    dir = Vector3.down * 100; // evade to down
                    break;
                default:
                    dir = TargetPos.position;
                    break;
            }
            
            targetVectors = FirstOrderIntercept(AircraftTransform.position, SAV.AirVel, currentSpeed,
                dir, air_combat_target.AirVel);
        }else 
        if (state_combat)
        {
            targetVectors = FirstOrderIntercept(AircraftTransform.position, SAV.AirVel, currentSpeed,
                TargetPos.position, air_combat_target.AirVel);
        }

        if (!state_takingoff && !state_taxiing)
        {
            if (raycastTerrainTimer < raycastTerrainTime)
            {
                raycastTerrainTimer = raycastTerrainTimer + Time.deltaTime;
            }
            else
            {
                raycastTerrainTimer = 0f;
                checkPullup(checkState);
                // if (!checkForward)
                // {
                //     if (Physics.Raycast(AircraftTransform.position, Vector3.down, out terrainCheck, minimumPullupDist * (currentSpeed / minimumPullupDist),
                //         TerrainLayers, QueryTriggerInteraction.Ignore))
                //     {
                //         if (Vector3.Distance(terrainCheck.point, AircraftTransform.position) / currentSpeed < Limit)
                //             state_pullingup = true;
                //     }
                //     else
                //     {
                //         state_pullingup = false;
                //     }
                //
                //     checkForward = true;
                // }
                // else
                // {
                //     if (Physics.Raycast(AircraftTransform.position, AircraftTransform.forward, out terrainCheck,
                //         minimumPullupDist * (currentSpeed / minimumPullupDist), //check forward
                //         TerrainLayers, QueryTriggerInteraction.Ignore))
                //     {
                //         if (Vector3.Distance(terrainCheck.point, AircraftTransform.position) / currentSpeed < Limit)
                //             state_pullingup = true;
                //     }
                //
                //     checkForward = false;
                // }
            }

            if (state_pullingup)
            {
                targetVectors.y = AircraftTransform.position.y + minimumPullupDist; // forcing to pull up for now. 
            }
        }


        // TO BE REFRACTORED
        float distance = Vector3.Distance(AircraftTransform.position, TargetPos.position);
        float checkUp = debug_pitchAngleDot =
            Vector3.Dot(AircraftTransform.up, targetVectors - AircraftTransform.position);
        float checkRight = debug_yawAngleDot =
            Vector3.Dot(AircraftTransform.right, targetVectors - AircraftTransform.position);

        float returnPitch = Mathf.Clamp(checkUp / ((MinimumAnglePitch * distance)), -1, 1);
        float returnYaw = Mathf.Clamp(checkRight / ((MinimumAngleYaw * distance)), -1, 1);
        Vector3 TargetDir = TargetPos.position - AircraftTransform.position;
        TargetDir.y = 0;
        float AngleToTarget = Vector3.Angle(AircraftTransform.forward, TargetDir);
        shouldRollAngle = Mathf.Clamp((checkRight / (MinimumAngleRoll * distance)) / rollStrength, -1, 1);
        if (AircraftTransform.localRotation.eulerAngles.z < maxRollAngle && checkUp < 0)
        {
            shouldRollAngle = 0;
        }
        else if (AircraftTransform.localRotation.eulerAngles.z > -maxRollAngle && checkUp > 0)
        {
            shouldRollAngle = 0;
        }

        debug_deltaAngle = Vector3.Angle(AircraftTransform.forward, targetVectors);

        PitchInput = Mathf.Lerp(PitchInput, -returnPitch, Time.deltaTime * TimeLerper);
        
        if (!state_taxiing && AngleToTarget > YawAngleMax)
        {
            YawInput = 0;
        }
        else
        {
            YawInput = Mathf.Lerp(YawInput, returnYaw, Time.deltaTime * TimeLerper);   
        }
        RollInput = Mathf.Lerp(RollInput, -shouldRollAngle, Time.deltaTime * TimeLerper); // needs to be inverted.
        // PitchInput = returnPitch;
        // YawInput = returnYaw; 
        // RollInput = -shouldRollAngle;

        // Gizmos.DrawLine(AircraftTransform.position, targetVectors);
        Debug.DrawLine(AircraftTransform.position, targetVectors, Color.blue);
    }

    public void checkPullup(int direction)
    {
        Vector3 dir;
        switch (direction)
        {
            case 0: dir = Vector3.down;
            break;
            case 1:dir = AircraftTransform.forward;
            break;
            case 2:dir = -AircraftTransform.right;
            break;
            case 3:dir = AircraftTransform.right;
            break;
            case 4:dir = AircraftTransform.up;
            break;
            default: dir = Vector3.down;
            break;
        }
        
        Debug.DrawLine(AircraftTransform.position, dir * minimumPullupDist * (currentSpeed / minimumPullupDist), Color.red);
        
        if (Physics.Raycast(AircraftTransform.position, dir, out terrainCheck, minimumPullupDist * (currentSpeed / minimumPullupDist),
            TerrainLayers, QueryTriggerInteraction.Ignore))
        {
            if (Vector3.Distance(terrainCheck.point, AircraftTransform.position) / currentSpeed < Limit)
            {
                state_pullingup = true;
                Debug.DrawLine(AircraftTransform.position, dir * minimumPullupDist, Color.yellow);
            }
        }
        else
        {
            if(direction==0)  state_pullingup = false;
        }

        checkState = checkState + 1;
        if (checkState > 4)
        {
            checkState = 0;
        }
    }


    //Tools
    public Vector3 FirstOrderIntercept(
        Vector3 shooterPosition,
        Vector3 shooterVelocity,
        float shotSpeed,
        Vector3 targetPosition,
        Vector3 targetVelocity)
    {
        Vector3 targetRelativePosition = targetPosition - shooterPosition;
        Vector3 targetRelativeVelocity = targetVelocity - shooterVelocity;

        if (targetVelocity != Vector3.zero)
        {
            float t = FirstOrderInterceptTime(
                shotSpeed,
                targetRelativePosition,
                targetRelativeVelocity
            );
            return targetPosition + t * (targetRelativeVelocity);
        }
        else
        {
            return targetPosition;
        }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        UpdateWaypoint();
    }

    public float FirstOrderInterceptTime(
        float shotSpeed,
        Vector3 targetRelativePosition,
        Vector3 targetRelativeVelocity)
    {
        float velocitySquared = targetRelativeVelocity.sqrMagnitude;
        if (velocitySquared < 0.001f)
            return 0f;

        float a = velocitySquared - shotSpeed * shotSpeed;

        //handle similar velocities
        if (Mathf.Abs(a) < 0.001f)
        {
            float t = -targetRelativePosition.sqrMagnitude /
                      (
                          2f * Vector3.Dot(
                              targetRelativeVelocity,
                              targetRelativePosition
                          )
                      );
            return Mathf.Max(t, 0f); //don't shoot back in time
        }

        float b = 2f * Vector3.Dot(targetRelativeVelocity, targetRelativePosition);
        float c = targetRelativePosition.sqrMagnitude;
        float determinant = b * b - 4f * a * c;

        if (determinant > 0f)
        {
            //determinant > 0; two intercept paths (most common)
            float t1 = (-b + Mathf.Sqrt(determinant)) / (2f * a),
                t2 = (-b - Mathf.Sqrt(determinant)) / (2f * a);
            if (t1 > 0f)
            {
                if (t2 > 0f)
                    return Mathf.Min(t1, t2); //both are positive
                else
                    return t1; //only t1 is positive
            }
            else
                return Mathf.Max(t2, 0f); //don't shoot back in time
        }
        else if (determinant < 0f) //determinant < 0; no intercept path
            return 0f;
        else //determinant = 0; one intercept path, pretty much never happens
            return Mathf.Max(-b / (2f * a), 0f); //don't shoot back in time
    }
}