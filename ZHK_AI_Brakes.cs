
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using SaccFlightAndVehicles;

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ZHK_AI_Brakes : UdonSharpBehaviour
    {

    /*
        /   Zhakami Modules - SAV AI BRAKES
        /   D: ZhakamiZhako#2147 | TW: @ZZhako | gmail: zhintamizhakami@gmail.com
        /   v 0.0001abcd - Fucking alpha of alpha's.
        /   For use in development and testing purposes. Not for redistribution outside Sacchill's Jet boys and/or selected persons
        /
        /       The current state of this shit is only to test how the planes behave during taxi, sound testing, flying as a dummy target, follow waypoints
        /  and to validate an aircraft's behaviour on certain things. This is not ready for world distribution quality; but rather this is meant only to 
        /  validate certain things such as damage handling, sound checking, target checking, taxiing and other things.
        /
        /   May be removed on the next update.
        /
        */


        public bool EnableAI = false;
        public DFUNC_Brake BRAKE_BASIS;
        public UdonSharpBehaviour SAVControl;
        [Tooltip("Looping sound to play while brake is active")]
        public AudioSource Airbrake_snd;
        [Tooltip("Will Crash if not set")]
        public Animator BrakeAnimator;
        [Tooltip("Position the ground brake force will be applied at")]
        public Transform GroundBrakeForcePosition;
        [Tooltip("Because you have to hold the break, and the keyboardcontrols script can only send events, this option is here.")]
        public KeyCode KeyboardControl = KeyCode.B;
        private bool UseLeftTrigger = false;
        [System.NonSerializedAttribute, UdonSynced(UdonSyncMode.None)] public float BrakeInput;
        private Rigidbody VehicleRigidbody;
        private bool HasAirBrake;
        public float AirbrakeStrength = 4f;
        public float GroundBrakeStrength = 6;
        [Tooltip("Water brake functionality requires that floatscript is being used")]
        public float WaterBrakeStrength = 1f;
        public bool NoPilotAlwaysGroundBrake = true;
        [Tooltip("Speed below which the ground break works meters/s")]
        public float GroundBrakeSpeed = 40f;
        //other functions can set this +1 to disable breaking
        [System.NonSerializedAttribute] public bool _DisableGroundBrake;
        [System.NonSerializedAttribute, FieldChangeCallback(nameof(DisableGroundBrake_))] public int DisableGroundBrake = 0;
        public int DisableGroundBrake_
        {
            set
            {
                _DisableGroundBrake = value > 0;
                DisableGroundBrake = value;
            }
            get => DisableGroundBrake;
        }
        private SaccEntity EntityControl;
        private float BrakeStrength;
        private int BRAKE_STRING = Animator.StringToHash("brake");
        private bool Braking;
        private bool BrakingLastFrame;
        private float LastDrag = 0;
        private float AirbrakeLerper;
        private float NonLocalActiveDelay;//this var is for adding a min delay for disabling for non-local users to account for lag
        private bool Selected;
        private bool IsOwner;
        private float NextUpdateTime;
        private float RotMultiMaxSpeedDivider;
        public float AIBrakeInput = 0f;
        public void DFUNC_LeftDial() { UseLeftTrigger = true; }
        public void DFUNC_RightDial() { UseLeftTrigger = false; }
        public void SFEXT_L_EntityStart()
        {
            if (BRAKE_BASIS != null)
            {
                SAVControl = BRAKE_BASIS.SAVControl;
                Airbrake_snd = BRAKE_BASIS.Airbrake_snd;
                BrakeAnimator = BRAKE_BASIS.BrakeAnimator;
                GroundBrakeForcePosition = BRAKE_BASIS.GroundBrakeForcePosition;
                AirbrakeStrength = BRAKE_BASIS.AirbrakeStrength;
                
                EntityControl = (SaccEntity)SAVControl.GetProgramVariable("EntityControl");
                VehicleRigidbody = EntityControl.GetComponent<Rigidbody>();
                HasAirBrake = AirbrakeStrength != 0;
                RotMultiMaxSpeedDivider = 1 / (float)SAVControl.GetProgramVariable("RotMultiMaxSpeed");
                IsOwner = (bool)SAVControl.GetProgramVariable("IsOwner");
                // VRCPlayerApi localPlayer = Networking.LocalPlayer;
                if (!GroundBrakeForcePosition) { GroundBrakeForcePosition = EntityControl.CenterOfMass; }   
                BRAKE_BASIS.gameObject.SetActive(false);
                // BRAKE_BASIS.enabled = false;
            }
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (localPlayer != null && !localPlayer.isMaster)
            { gameObject.SetActive(false); }
            else
            { gameObject.SetActive(true); }
        }
        // public void DFUNC_Selected()
        // {
        //     Selected = true;
        // }
        // public void DFUNC_Deselected()
        // {
        //     BrakeInput = 0;
        //     Selected = false;
        // }
        public void ZHK_AI_ENABLE()
        {
            EnableAI = true;
        }

        public void ZHK_AI_DISABLE()
        {
            EnableAI = false;
            BRAKE_BASIS.enabled = true;
            BRAKE_BASIS.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }
        public void SFEXT_O_PilotEnter()
        {
            if (!NoPilotAlwaysGroundBrake)
            {
                if ((bool)SAVControl.GetProgramVariable("Floating"))
                {
                    BrakeStrength = WaterBrakeStrength;
                }
                else if ((bool)SAVControl.GetProgramVariable("Taxiing"))
                {
                    BrakeStrength = GroundBrakeStrength;
                }
            }
        }
        public void SFEXT_O_PilotExit()
        {
            BrakeInput = 0;
            RequestSerialization();
            Selected = false;
            if (!NoPilotAlwaysGroundBrake)
            { BrakeStrength = 0; }
        }
        public void SFEXT_G_Explode()
        {
            BrakeInput = 0;
            BrakeAnimator.SetFloat(BRAKE_STRING, 0);
        }
        public void SFEXT_O_TakeOwnership()
        {
            gameObject.SetActive(true);
            IsOwner = true;
        }
        public void SFEXT_O_LoseOwnership()
        {
            gameObject.SetActive(false);
            IsOwner = false;
        }
        public void EnableForAnimation()
        {
            if (!IsOwner)
            {
                if (Airbrake_snd) { Airbrake_snd.Play(); }
                gameObject.SetActive(true);
                NonLocalActiveDelay = 3;
            }
        }
        public void DisableForAnimation()
        {
            BrakeAnimator.SetFloat(BRAKE_STRING, 0);
            BrakeInput = 0;
            AirbrakeLerper = 0;
            if (Airbrake_snd)
            {
                Airbrake_snd.pitch = 0;
                Airbrake_snd.volume = 0;
            }
            gameObject.SetActive(false);
        }
        public void SFEXT_G_TouchDownWater()
        {
            BrakeStrength = WaterBrakeStrength;
        }
        public void SFEXT_G_TouchDown()
        {
            BrakeStrength = GroundBrakeStrength;
        }
        private void Update()
        {
            float DeltaTime = Time.deltaTime;
            if (IsOwner)
            {
                float Speed = (float)SAVControl.GetProgramVariable("Speed");
                Vector3 CurrentVel = (Vector3)SAVControl.GetProgramVariable("CurrentVel");
                bool Taxiing = (bool)SAVControl.GetProgramVariable("Taxiing");
                if (EnableAI)
                {
                    BrakeInput = AIBrakeInput;
                    if (Taxiing)
                    {
                        //ground brake checks if vehicle is on top of a rigidbody, and if it is, brakes towards its speed rather than zero
                        //does not work if owner of vehicle does not own the rigidbody 
                        Rigidbody gdhr = (Rigidbody)SAVControl.GetProgramVariable("GDHitRigidbody");
                        if (gdhr)
                        {
                            float RBSpeed = ((Vector3)SAVControl.GetProgramVariable("CurrentVel") - gdhr.velocity).magnitude;
                            if (BrakeInput > 0 && RBSpeed < GroundBrakeSpeed && !_DisableGroundBrake)
                            {
                                Vector3 speed = (VehicleRigidbody.GetPointVelocity(GroundBrakeForcePosition.position) - gdhr.velocity).normalized;
                                speed = Vector3.ProjectOnPlane(speed, EntityControl.transform.up);
                                Vector3 BrakeForce = speed.normalized * BrakeInput * BrakeStrength * DeltaTime;
                                if (speed.sqrMagnitude < BrakeForce.sqrMagnitude)
                                { BrakeForce = speed; }
                                VehicleRigidbody.AddForceAtPosition(-speed * BrakeInput * BrakeStrength * DeltaTime, GroundBrakeForcePosition.position, ForceMode.VelocityChange);
                            }
                        }
                        else
                        {
                            if (BrakeInput > 0 && Speed < GroundBrakeSpeed && !_DisableGroundBrake)
                            {
                                Vector3 speed = VehicleRigidbody.GetPointVelocity(GroundBrakeForcePosition.position);
                                speed = Vector3.ProjectOnPlane(speed, EntityControl.transform.up);
                                Vector3 BrakeForce = speed.normalized * BrakeInput * BrakeStrength * DeltaTime;
                                if (speed.sqrMagnitude < BrakeForce.sqrMagnitude)
                                { BrakeForce = speed; }//this'll stop the vehicle exactly
                                VehicleRigidbody.AddForceAtPosition(-BrakeForce, GroundBrakeForcePosition.position, ForceMode.VelocityChange);
                            }
                        }
                    }
                    // if (!HasAirBrake && !(bool)SAVControl.GetProgramVariable("Taxiing"))
                    // {
                    //     BrakeInput = 0;
                    // }
                    //remove the drag added last frame to add the new value for this frame
                    float extradrag = (float)SAVControl.GetProgramVariable("ExtraDrag");
                    float newdrag = (AirbrakeStrength * BrakeInput);
                    float dragtoadd = -LastDrag + newdrag;
                    extradrag += dragtoadd;
                    LastDrag = newdrag;
                    SAVControl.SetProgramVariable("ExtraDrag", extradrag);

                    //send events to other users to tell them to enable the script so they can see the animation
                    Braking = BrakeInput > .02f;
                    if (Braking)
                    {
                        if (!BrakingLastFrame)
                        {
                            if (Airbrake_snd && !Airbrake_snd.isPlaying) { Airbrake_snd.Play(); }
                            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(EnableForAnimation));
                        }
                        if (Time.time > NextUpdateTime)
                        {
                            RequestSerialization();
                            NextUpdateTime = Time.time + .4f;
                        }
                    }
                    else
                    {
                        if (BrakingLastFrame)
                        {
                            float brk = BrakeInput;
                            BrakeInput = 0;
                            RequestSerialization();
                            BrakeInput = brk;
                        }
                    }
                    if (AirbrakeLerper < .03 && BrakeInput < .03)
                    {
                        if (Airbrake_snd && Airbrake_snd.isPlaying) { Airbrake_snd.Stop(); }
                    }
                    BrakingLastFrame = Braking;
                }
                else
                {
                    if (Taxiing)
                    {
                        //outside of vehicle, simpler version, ground brake always max
                        Rigidbody gdhr = null;
                        { gdhr = (Rigidbody)SAVControl.GetProgramVariable("GDHitRigidbody"); }
                        if (gdhr)
                        {
                            float RBSpeed = ((Vector3)SAVControl.GetProgramVariable("CurrentVel") - gdhr.velocity).magnitude;
                            if (RBSpeed < GroundBrakeSpeed && !_DisableGroundBrake)
                            {
                                VehicleRigidbody.velocity = Vector3.MoveTowards(VehicleRigidbody.velocity, gdhr.GetPointVelocity(EntityControl.CenterOfMass.position), BrakeStrength * DeltaTime);
                            }
                        }
                        else
                        {
                            if (Speed < GroundBrakeSpeed && !_DisableGroundBrake)
                            {
                                VehicleRigidbody.velocity = Vector3.MoveTowards(VehicleRigidbody.velocity, Vector3.zero, BrakeStrength * DeltaTime);
                            }
                        }
                    }
                }
            }
            else
            {
                //this object is enabled for non-owners only while animating
                NonLocalActiveDelay -= DeltaTime;
                if (NonLocalActiveDelay < 0 && AirbrakeLerper < 0.01)
                {
                    DisableForAnimation();
                    return;
                }
            }
            AirbrakeLerper = Mathf.Lerp(AirbrakeLerper, BrakeInput, 2f * DeltaTime);
            BrakeAnimator.SetFloat(BRAKE_STRING, AirbrakeLerper);
            if (Airbrake_snd)
            {
                Airbrake_snd.pitch = AirbrakeLerper * .2f + .9f;
                Airbrake_snd.volume = AirbrakeLerper * Mathf.Min((float)SAVControl.GetProgramVariable("Speed") * RotMultiMaxSpeedDivider, 1);
            }
        }
    }