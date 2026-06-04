#include "pch.h"
#include "image_lib.h"

extern "C" {

__declspec(dllexport) void encode_gray(int *f, int w, int h, int d, int *g) {
  if (d == 3) { // is BGR img
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * 3;
        double b_val = f[idx + 0];
        double g_val = f[idx + 1];
        double r_val = f[idx + 2];
        int gray_val = (int)(r_val * 0.299 + g_val * 0.587 + b_val * 0.114);
        g[idx + 0] = gray_val;
        g[idx + 1] = gray_val;
        g[idx + 2] = gray_val;
      }
    }
  } else if (d == 1) { // direct output
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = y * w + x;
        g[idx] = f[idx];
      }
    }
  }
}

__declspec(dllexport) void bit_plane_slice(int *f, int w, int h, int d, int *g,
                                           int plane, int binarize) {
  if (!f || !g || w <= 0 || h <= 0 || plane < 0 || plane > 7) {
    return;
  }
  if (d != 1 && d != 3) {
    return;
  }

  int mask = 1 << plane;

  for (int j = 0; j < h; j++) {
    for (int i = 0; i < w; i++) {
      if (d == 1) {
        int idx = j * w + i;
        int pixel = f[idx];
        int bitVal = pixel & mask;
        g[idx] = (binarize != 0) ? (bitVal ? 255 : 0) : bitVal;
      } else if (d == 3) {
        int idx = (j * w + i) * 3;
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        int b_bit = b_val & mask;
        int g_bit = g_val & mask;
        int r_bit = r_val & mask;

        if (binarize != 0) {
          g[idx + 0] = b_bit ? 255 : 0;
          g[idx + 1] = g_bit ? 255 : 0;
          g[idx + 2] = r_bit ? 255 : 0;
        } else {
          g[idx + 0] = b_bit;
          g[idx + 1] = g_bit;
          g[idx + 2] = r_bit;
        }
      }
    }
  }
}

}
