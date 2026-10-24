// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Source Digital / Race fragment equations. Static atlas AA is an approximation
// of screen-space derivatives; source procedural topology is unchanged.
Shader "Hidden/OpenBrush/DigitalBake" {
 SubShader { Pass {
  Cull Off ZWrite Off ZTest Always Blend Off
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma target 3.0
  #include "UnityCG.cginc"
  #define BRUSHES_DIGITAL_ROWS 5
  float _Race,_Copies; static const float _EmissionGain=.5; static const float _Opacity=1;
  struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
  struct Varying {float4 pos:SV_POSITION;float2 st:TEXCOORD0;float4 color:COLOR;};
  Varying vert(Input v) {
   Varying o;o.pos=float4(v.vertex.xy*2-1,0,1);
   #if UNITY_UV_STARTS_AT_TOP
    o.pos.y=-o.pos.y;
   #endif
   o.st=v.uv*float2(25,5);o.color=v.color;return o;
  }
float4 bloomColor(float4 color, float gain) {
  // Guarantee that there's at least a little bit of all 3 channels.
  // This makes fully-saturated strokes (which only have 2 non-zero
  // color channels) eventually clip to white rather than to a secondary.
  float cmin = length(color.rgb) * .05;
  color.rgb = max(color.rgb, float3(cmin, cmin, cmin));
  // If we try to remove this pow() from .a, it brightens up
  // pressure-sensitive strokes; looks better as-is.
  color = pow(color, 2.2);
  color.rgb *= 2 * exp(gain * 10);
  return color;
}

float random(float2 st) {
  return frac(sin(dot(st.xy,
      float2(12.9898,78.233)))*
      43758.5453123);
}

  float4 frag(Varying i):SV_Target {
        float stroke_width = .1;
        float antialias_feather_px = 8;

        float2 st = i.st;

        // used for antialiasing
        float2 st_per_px = fwidth(st);

        // create the grid.
        // rc means row,col. rc.x actually really means x i.e. it's columns (from the perspective
        // of a stroke going from left to right in front of you).
        float2 rc = floor(st);

        // get the tile coordinates
        // map tile_st.y to [-1, 1]
        float2 tile_st = frac(st);
        tile_st -= .5;
        tile_st *= 2;

        float lum = 0;
        float r = length(tile_st);

        // for each neighboring tile...
        for (int ii = -1; ii <= 1; ii++) {
          if (rc.x + ii < 0) continue;
          for (int jj = -1; jj <= 1; jj++) {
            if (abs(ii) == abs(jj)) continue;
            if (rc.y + jj < 0) continue;
            if (rc.y + jj >= BRUSHES_DIGITAL_ROWS) continue;

            // me and my neighbor decide if we should connect our tiles
            //
            // (x) represents random(rc)
            // [x] represents random(rc) + random(rc + ij)
            //
            // (0.5)xx[1.3]xx(0.8)
            //   x
            //   x
            // [1.1]         [0.4]
            //   x
            //   x
            // (0.6)  [0.7]  (0.1)
            //
            // if i'm (0.5), then i'm going to draw (0.5)xxx---(0.8)
            // if i'm (0.8), then i'm going to draw (0.5)---xxx(0.8)
            if (random(rc) + random(rc + float2(ii, jj)) < (_Race > .5 ? (ii == 0 ? 1.5 : .5) : 1)) continue;

            // draw a rectangle with width stroke_width through the center of
            // this tile towards the other tile
            //      i=0,j=1   i=-1,j=0
            //    ( --111--   ------- )   --111--
            //    ( --111--   ------- )   --111--
            //    ( --111--   11111-- )   11111--
            // max( --111-- , 11111-- ) = 11111--
            //    ( --111--   11111-- )   11111--
            //    ( -------   ------- )   -------
            //    ( -------   ------- )   -------

            float2 ij = float2(ii, jj);
            float2 ij_perp = float2(-jj, ii);
            // when ij points to the right:
            // bound1 is bottomleft
            // bound2 is topright
            float2 bound1 = stroke_width * -ij_perp + stroke_width * -ij;
            float2 bound2 = ij + stroke_width * ij_perp;
            float2 min_bound = min(bound1, bound2);
            float2 max_bound = max(bound1, bound2);

            float2 aa_feather = st_per_px * antialias_feather_px;
            float current_lum =
                smoothstep(min_bound.x - aa_feather.x, min_bound.x, tile_st.x) *
                smoothstep(max_bound.x + aa_feather.x, max_bound.x, tile_st.x) *
                smoothstep(min_bound.y - aa_feather.y, min_bound.y, tile_st.y) *
                smoothstep(max_bound.y + aa_feather.y, max_bound.y, tile_st.y);
            lum = max(lum, current_lum);
          }
        }
        float4 color;
        color.a = 1;
        color.rgb = lum*bloomColor(i.color,lum*_EmissionGain);
        float3 rgb=max(color.rgb * _Opacity * _Copies,0);
        float3 rgbLinear=lerp(GammaToLinearSpace(saturate(rgb)),rgb,step(1,rgb));
        return float4(LinearToGammaSpace(min(rgbLinear,4)/4),1);
  }
 ENDCG
 } }
}



