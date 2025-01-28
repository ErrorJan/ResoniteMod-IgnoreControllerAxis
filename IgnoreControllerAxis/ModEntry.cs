using System.Collections.Generic;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System.Reflection.Emit;
using System;
using System.Reflection;
using Elements.Core;
using Valve.VR;

namespace IgnoreControllerAxis;

public static class QuickExtensions
{
    public static float2 Clamp( this float2 f, float2 min, float2 max )
    {
        return new float2( Math.Min( Math.Max( f.X, min.X ), max.X ), Math.Min( Math.Max( f.Y, min.Y ), max.Y ) );
    }
}

/// This mod is slightly overengineered...
/// But it works!
/// This mod was made really quickly, so some things might look weird...

public class IgnoreControllerAxis : ResoniteMod {
    internal const string VERSION_CONSTANT = "1.0.0"; 
    public override string Name => "Ignore Controller Axis";
    public override string Author => "ErrorJan";
    public override string Version => VERSION_CONSTANT;
    public override string Link => "https://github.com/ErrorJan/ResoniteMod-IgnoreControllerAxis";
    private static ModConfiguration? rmlConfig;

    public override void OnEngineInit() 
    {
        rmlConfig = GetConfiguration();
        rmlConfig?.Save( true );

        Harmony harmony = new Harmony("ErrorJan.IgnoreControllerAxis");
        harmony.PatchAll();

        SteamVRDriver_UpdateController_Patch.Patch( harmony );
    }

    [AutoRegisterConfigKey]
    private static readonly 
        ModConfigurationKey<bool> modEnabled = 
            new("Enabled", 
                "Should the mod be enabled?", 
                () => true);
    
    [AutoRegisterConfigKey]
    private static readonly 
        ModConfigurationKey<float2> rightControllerAxisMultiplier = 
            new("Right Controller Axis Multiplier", 
                "Set an axis to 0 to disable that direction completely. (Right Controller)", 
                () => new float2( 1, 1 ));
    [AutoRegisterConfigKey]
    private static readonly 
        ModConfigurationKey<float2> leftControllerAxisMultiplier = 
            new("Left Controller Axis Multiplier", 
                "Set an axis to 0 to disable that direction completely. (Left Controller)", 
                () => new float2( 1, 1 ));
    [AutoRegisterConfigKey]
    private static readonly 
        ModConfigurationKey<float2> rightControllerAxisDeadzone = 
            new("Right Controller Axis Deadzone", 
                "Right Controller Axis Deadzone (Value from 0 - 100)", 
                () => new float2( 1, 1 ));
    [AutoRegisterConfigKey]
    private static readonly 
        ModConfigurationKey<float2> leftControllerAxisDeadzone = 
            new("Left Controller Axis Deadzone", 
                "Left Controller Axis Deadzone (Value from 0 - 100)", 
                () => new float2( 1, 1 ));

    class SteamVRDriver_UpdateController_Patch
    {
        private static string currentController = "Undefined";

        public static void Patch( Harmony harmony )
        {

            MethodInfo[] methods = new[]
            {
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( IndexController ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( ViveController ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( WindowsMRController ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( TouchController ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( HPReverbController ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( PicoNeo2Controller ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( CosmosController ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
                AccessTools.Method( typeof( SteamVRDriver ), "UpdateController", new Type[] { 
                    typeof( GenericController ), 
                    typeof( Hand ), 
                    typeof( SteamVR_Input_Sources ), 
                    typeof( float ) 
                } ),
            };

            string[] controllers = new [] {
                "IndexController",
                "ViveController",
                "WindowsMRController",
                "TouchController",
                "HPReverbController",
                "PicoNeo2Controller",
                "CosmosController",
                "GenericController",
            };

            int i = 0;
            HarmonyMethod harmonyPatch = new ( SteamVRDriver_UpdateController_Patch.Transpiler );
            foreach( MethodInfo method in methods )
            {
                currentController = controllers[i++];
                harmony.Patch( method, transpiler: harmonyPatch );
            }
        }

        public static float2 BlockAxisInput( float2 axis, Hand hand )
        {
            if ( rmlConfig == null || !rmlConfig.GetValue( modEnabled ) )
                return axis;

            float2 axisMul = hand.Chirality == Chirality.Right ? rmlConfig.GetValue( rightControllerAxisMultiplier ) : rmlConfig.GetValue( leftControllerAxisMultiplier );
            float2 axisDeadzone = hand.Chirality == Chirality.Right ? rmlConfig.GetValue( rightControllerAxisDeadzone ) : rmlConfig.GetValue( leftControllerAxisDeadzone );

            axisDeadzone = axisDeadzone.Clamp( float2.Zero, float2.One * 100f ) / 100f;

            bool xN = axis.X < 0f;
            bool yN = axis.Y < 0f;

            float x = Math.Max( ( Math.Abs( axis.X ) - axisDeadzone.X ) / ( 1 - axisDeadzone.X ), 0f );
            float y = Math.Max( ( Math.Abs( axis.Y ) - axisDeadzone.Y ) / ( 1 - axisDeadzone.Y ), 0f );

            x = xN ? -x : x;
            y = yN ? -y : y;

            axis = axis.SetY( y * axisMul.Y );
            axis = axis.SetX( x * axisMul.X );

            return axis;
        }

        public static IEnumerable<CodeInstruction> Transpiler( IEnumerable<CodeInstruction> instructions )
        {
            Msg( $"Patching UpdateController for { currentController }..." );

            MethodInfo method_ElementsCoreMathX_RadialDeadzone = AccessTools.Method( typeof( MathX ), "RadialDeadzone", new Type[] { typeof( float2 ).MakeByRefType(), typeof( float ) } );
            MethodInfo method_BlockAxisInput = SymbolExtensions.GetMethodInfo( () => BlockAxisInput );

            foreach ( var instruction in instructions )
            {
                // Very naiive approach, but works for now
                // After the RadialDeadzone method call, we inject our own function.
                // ldarg_2 will have the Hand object stored.
                if ( instruction.Calls( method_ElementsCoreMathX_RadialDeadzone ) )
                {
                    yield return instruction;
                    yield return new CodeInstruction( OpCodes.Ldarg_2 );
                    yield return new CodeInstruction( OpCodes.Call, method_BlockAxisInput );
                    Msg( "Patched!" );
                    continue;
                }

                yield return instruction;
            }
        }
    }
}
