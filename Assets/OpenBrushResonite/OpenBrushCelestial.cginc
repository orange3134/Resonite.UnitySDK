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

// Extracted normal Fairy and Space fragment equations; no audio input.
#include "Editor/BubbleNoiseReference.cginc"
#include "OpenBrushColorSpaceReference.cginc"
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

float remap(float x1,float x2,float y1,float y2,float x) {
  return y1 + (y2-y1)*(x-x1)/(x2-x1);
}
float clampedRemap(float x1,float x2,float y1,float y2, float x)
{
  float t = clamp(x1,x2,x);
  return remap(x1,x2,y1,y2,t);
}
float4 FairyFragment(float2 uv,float4 paint,float seconds) {
float scale1 = 3;
        float scale2 = 3;

        float2 st = uv.xy;
        

        // fix aspect ratio
        st.x *= 5;

        // divide the space into tiles
        float2 scaler = floor(st);
        scaler = random(scaler);
        scaler *= scale1;
        scaler = max(scaler, 1);
        scaler = floor(scaler);
        st *= scaler;

        // and again
        scaler = floor(st);
        scaler = random(scaler + 234.4);
        scaler *= scale2;
        scaler = max(scaler, 1);
        scaler = floor(scaler);
        st *= scaler;

        // row,col (only used as random seed)
        float2 rc = floor(st);

        // get the tile uv
        st = frac(st);
        st -= .5;
        st *= 2;

        // scale it a bit
        float rscale = lerp(.2, 1, random(rc));
        st /= rscale;

        // move it a little bit
        float2 offset = random2(rc + 5) * .1;
        st += offset;

        float r = length(st);
        float lum = 1 - r;

        // make sure the dot fully fades by the time we get to the edge
        // of the square, otherwise it will get chopped off
        lum -= max(offset.x, offset.y);
        lum = saturate(lum);

        // vary the radial brightness falloff
        float powpow = random(rc);
        powpow = powpow * 2 - 1;
        powpow = max(.3, powpow);
        if (powpow < 0)
        {
            powpow = 1 / abs(powpow);
        }
        lum *= 2;
        lum = pow(lum, powpow);

        // fade the dots in and out with variety
        float fadespeed = lerp(.25, 1.25, random(rc));
        float fadephase = random(rc) * 2 * 3.14;
        float time = sin((seconds*2) * fadespeed + fadephase) / 2 + .5;
        lum *= lerp(0, 1, time);

        fixed4 color;
        color.a = 1;
        color.rgb = lum*bloomColor(paint,lum*.5);
        return color;
      
}
float4 SpaceFragment(float2 uv,float4 paint,float seconds) {
float analog_spread = .1;  // how far the analogous hues are from the primary
        float gain = 10;
        float gain2 = 0;

        // primary hue is chosen by user
        float3 i_HSV = RGBtoHSV(paint.rgb);

        // we're gonna mix these 3 colors together
        float primary_hue = i_HSV.x;
        float analog1_hue = frac(primary_hue - analog_spread);
        float analog2_hue = frac(primary_hue + analog_spread);

        float r = abs(uv.y * 2 - 1);  // distance from center of stroke

        // determine the contributions of each hue
        float primary_a = .2 * fbm(uv + (seconds*.05)) * gain + gain2;
        float analog1_a = .2 * fbm(float3(uv.x + 12.52, uv.y + 12.52, (seconds*.05) * 5.2)) * gain + gain2;
        float analog2_a = .2 * fbm(float3(uv.x + 6.253, uv.y + 6.253, (seconds*.05) * .8)) * gain + gain2;

        // the main hue is present in the center and falls off with randomized radius
        primary_a = clampedRemap(0, .5, primary_a, 0, r + fbm(float2((seconds*.05) + 50, uv.x)) * 2);

        // the analog hues start a little out from the center and increase with intensity going out
        analog1_a = clampedRemap(.2, 1, 0, analog1_a * 1.2, r);
        analog2_a = clampedRemap(.2, 1, 0, analog2_a * 1.2, r);

        fixed4 color;
        color.a = primary_a + analog1_a + analog2_a;

        float final_hue =
          primary_a * (primary_hue) +
          analog1_a * (analog1_hue) +
          analog2_a * (analog2_hue);
        final_hue /= color.a;

        // now sculpt the overall shape of the stroke
        float lum = 1 - r;
        float rfbm = fbm(float2(uv.x, (seconds*.05)));
        rfbm += 1.2;
        rfbm *= .8;
        lum *= step(r, rfbm);  // shorten the radius with fbm

        // blur the edge a little bit
        lum *= smoothstep(rfbm, rfbm - .2, r);

        color.rgb = HSVToRGB(float3(final_hue, i_HSV.y, i_HSV.z * lum));
        color = saturate(color);  // i'm not sure why it's so bright without this
        color = bloomColor(color,.5);



        return color;
      
}
