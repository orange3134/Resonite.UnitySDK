using System;
using FrooxEngine;

// The SDK currently sends an open generic name for AssetMultiplexer<T>.
// Use the runtime-reflected closed name without editing generated SDK bindings.
[Serializable]
[ResoniteTypeName("[FrooxEngine]FrooxEngine.AssetMultiplexer<[FrooxEngine]FrooxEngine.ITexture2D>")]
public class OpenBrushTextureMultiplexer : AssetMultiplexer<ITexture2D> { }
