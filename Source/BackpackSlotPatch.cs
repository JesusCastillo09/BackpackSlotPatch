using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace BackpackSlotPatch
{
    //Clase estática que aplica parches de Harmony al juego.
    [StaticConstructorOnStartup]
    public static class HarmonyPatcher
    {
        static HarmonyPatcher()
        {
            var harmony = new Harmony("blatserts.backpackslotpatch");
            harmony.PatchAll();
            Log.Message("[BackpackSlotPatch] Harmony patches applied.");
        }
    }

    //Definición de las capas de ropa utilizadas en el parche.
    [DefOf]
    public static class ApparelLayerDefOf
    {
        public static ApparelLayerDef Back;
        public static ApparelLayerDef Belt;

        static ApparelLayerDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ApparelLayerDefOf));
        }
    }

    //Clase estática que se ejecuta al inicio del juego para modificar las definiciones de ropa.
    [StaticConstructorOnStartup]
    public static class BackpackSlotInjector
    {
        static BackpackSlotInjector()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                //Recorre todos los ThingDef cargados del juego.
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    try
                    {
                        //Verifica si la definición es de ropa y tiene la etiqueta "RH2_Backpack".
                        if (def.apparel == null || def.apparel.tags == null || !def.apparel.tags.Contains("RH2_Backpack"))
                            continue;
                        //Asugura que la capa "Back" esté inicializada.
                        if (ApparelLayerDefOf.Back == null)
                        {
                            Log.Error("[BackpackSlotPatch] La capa de ropa 'Back' no está inicializado.");
                            continue;
                        }
                        //Inicializa la lista de capas si es nula.
                        if (def.apparel.layers == null)
                            def.apparel.layers = new List<ApparelLayerDef>();
                        bool hasBelt = def.apparel.layers.Contains(ApparelLayerDefOf.Belt);
                        bool hasBack = def.apparel.layers.Contains(ApparelLayerDefOf.Back);
                        //Modifica las capas de ropa según las condiciones.
                        if (hasBelt && !hasBack)
                        {
                            def.apparel.layers.Remove(ApparelLayerDefOf.Belt);
                            def.apparel.layers.Add(ApparelLayerDefOf.Back);
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[BackpackSlotPatch] Añadida la capa Back a {def.defName}");
                            }
                        }
                        else if (!hasBelt && !hasBack)
                        {
                            def.apparel.layers.Add(ApparelLayerDefOf.Back);
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[BackpackSlotPatch] Añadida la capa Back a {def.defName}");
                            }
                        }
                        //Copiar atributos originales de wornGraphicData (Si existen) para no perder datos de los offset y scales.
                        if (def.apparel.wornGraphicData != null)
                        {
                            var originalWornGraphicData = def.apparel.wornGraphicData;
                            def.apparel.wornGraphicData = new WornGraphicData
                            {
                                renderUtilityAsPack = originalWornGraphicData.renderUtilityAsPack,
                                female = originalWornGraphicData.female,
                                male = originalWornGraphicData.male,
                                north = originalWornGraphicData.north,
                                east = originalWornGraphicData.east,
                                south = originalWornGraphicData.south,
                                west = originalWornGraphicData.west,
                            };
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[BackpackSlotPatch] Copiando wornGraphicData para {def.defName}");
                            }
                        }
                        else //Si no existe wornGraphicData, crea una instacia basica para evitar null.
                        {
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[BackpackSlotPatch] wornGraphicData no encontrado para {def.defName}");
                            }
                            def.apparel.wornGraphicData = new WornGraphicData
                            {
                                renderUtilityAsPack = true
                            };
                        }
                        //Asegura que el shader y el drawSize estén configurados correctamente.
                        if(def.graphicData != null)
                        {
                            if(Prefs.DevMode)
                            {
                                Log.Message($"[BackpackSlotPatch] graphicData encontrado para {def.defName}");
                            }
                            if (def.graphicData.shaderType == null)
                                def.graphicData.shaderType = ShaderTypeDefOf.CutoutComplex;
                            if(def.graphicData.graphicClass == null)
                                def.graphicData.graphicClass = typeof(Graphic_Multi);
                            if (def.graphicData.drawSize == Vector2.zero)
                                def.graphicData.drawSize = new Vector2(1f, 1f);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"[BackpackSlotPatch] Error al procesar def {(def?.defName ?? "null")}: {ex}");
                    }
                }
                Log.Message("[BackpackSlotPatch] Definiciones de ropa modificadas para usar la capa Back.");
            });
        }
    }

    //Parche de Harmony para modificar el comportamiento del método TryGetGraphicApparel.
    [HarmonyPatch(typeof(ApparelGraphicRecordGetter), nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel))]
    public static class Patch_ApparelGraphicRecordGetter
    {
        static bool Prefix(Apparel apparel, ref ApparelGraphicRecord rec, ref bool __result)
        {
            //Verifica si la prenda está asignada a la capa "Back".
            if (apparel.def.apparel.layers.Any(l => l.defName == "Back"))
            {
                var graphic = apparel.Graphic;
                if (graphic == null)
                {
                    Log.Warning($"[BackpackSlotPatch] No se encontró gráfico para: {apparel.def.defName}");
                    __result = false;
                    return false;
                }
                rec = new ApparelGraphicRecord(graphic, apparel);
                __result = true;
                return false;//Salta la ejecución original del método.
            }
            return true;//Permite la ejecución original del método si no se cumple la condición.
        }
    }
}