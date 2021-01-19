float3 SampleSpecularBRDF(BSDFData bsdfData, float2 sample, float3 viewWS)
{
    float roughness = PerceptualRoughnessToRoughness(bsdfData.perceptualRoughness);
    float3x3 localToWorld;
    if (!HasFlag(bsdfData.materialFeatures, MATERIALFEATUREFLAGS_FABRIC_COTTON_WOOL))
    {
        localToWorld = float3x3(bsdfData.tangentWS, bsdfData.bitangentWS, bsdfData.normalWS);
    }
    else
    {
        localToWorld = GetLocalFrame(bsdfData.normalWS);
    }
    float NdotL, NdotH, VdotH;
    float3 sampleDir;
    SampleGGXDir(sample, viewWS, localToWorld, roughness, sampleDir, NdotL, NdotH, VdotH);
    return sampleDir;
}

#ifdef HAS_LIGHTLOOP
IndirectLighting EvaluateBSDF_RaytracedReflection(LightLoopContext lightLoopContext,
                                                    BSDFData bsdfData,
                                                    PreLightData preLightData,
                                                    float3 reflection,
                                                    inout float reflectionHierarchyWeight,
                                                    inout LightHierarchyData lightHierarchyData)
{
    IndirectLighting lighting;
    ZERO_INITIALIZE(IndirectLighting, lighting);
    lighting.specularReflected = reflection.rgb * preLightData.specularFGD;
    return lighting;
}


IndirectLighting EvaluateBSDF_RaytracedRefraction(LightLoopContext lightLoopContext,
                                                    PreLightData preLightData,
                                                    float3 transmittedColor)
{
    IndirectLighting lighting;
    ZERO_INITIALIZE(IndirectLighting, lighting);
    return lighting;
}

float RecursiveRenderingReflectionPerceptualSmoothness(BSDFData bsdfData)
{
    return PerceptualRoughnessToPerceptualSmoothness(bsdfData.perceptualRoughness);
}
#endif

#if (SHADERPASS == SHADERPASS_RAYTRACING_GBUFFER)
void FitToStandardLit( SurfaceData surfaceData
                        , BuiltinData builtinData
                        , uint2 positionSS
                        , out StandardBSDFData outStandardlit)
{
    outStandardlit.baseColor = surfaceData.baseColor;
    outStandardlit.specularOcclusion = surfaceData.specularOcclusion;
    outStandardlit.normalWS = surfaceData.normalWS;
    outStandardlit.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surfaceData.perceptualSmoothness);
    outStandardlit.fresnel0 = surfaceData.specularColor;
    outStandardlit.coatMask = 0.0;
    outStandardlit.emissiveAndBaked = builtinData.bakeDiffuseLighting * surfaceData.ambientOcclusion + builtinData.emissiveColor;
    outStandardlit.isUnlit = 0;
}
#endif
