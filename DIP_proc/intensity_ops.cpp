#include "pch.h"
#include <cmath>
#include "image_lib.h"

extern "C" {

__declspec(dllexport) void adjust_brightness_contrast(int *f, int w, int h,
                                                      int d, int *g,
                                                      double alpha, int beta) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || (d != 1 && d != 3)) {
    return;
  }

  int lut[256];
  if (alpha >= 0.0) {
    // Linear brightness & contrast adjustment
    for (int i = 0; i < 256; i++) {
      double val = alpha * (double)i + (double)beta;
      int rounded_val = (int)(val + (val >= 0.0 ? 0.5 : -0.5));
      if (rounded_val < 0)
        rounded_val = 0;
      if (rounded_val > 255)
        rounded_val = 255;
      lut[i] = rounded_val;
    }
  } else {
    // Non-linear Gamma correction
    double gamma = -alpha;
    lut[0] = 0;
    for (int i = 1; i < 256; i++) {
      double val = 255.0 * std::pow((double)i / 255.0, gamma);
      int rounded_val = (int)(val + 0.5);
      if (rounded_val < 0)
        rounded_val = 0;
      if (rounded_val > 255)
        rounded_val = 255;
      lut[i] = rounded_val;
    }
  }

  int total_elements = (d == 1) ? (w * h) : (w * h * 3);
  for (int i = 0; i < total_elements; i++) {
    int pixel_val = f[i];
    if (pixel_val < 0)
      pixel_val = 0;
    if (pixel_val > 255)
      pixel_val = 255;
    g[i] = lut[pixel_val];
  }
}

__declspec(dllexport) void calculate_histogram(int *f, int w, int h, int d,
                                               int *histB, int *histG,
                                               int *histR) {
  // Initialize histograms to 0
  if (histB) {
    for (int i = 0; i < 256; i++)
      histB[i] = 0;
  }
  if (histG) {
    for (int i = 0; i < 256; i++)
      histG[i] = 0;
  }
  if (histR) {
    for (int i = 0; i < 256; i++)
      histR[i] = 0;
  }

  if (d == 1) {
    if (histB) {
      for (int y = 0; y < h; y++) {
        for (int x = 0; x < w; x++) {
          int idx = y * w + x;
          int gray_val = f[idx];
          if (gray_val >= 0 && gray_val <= 255) {
            histB[gray_val]++;
          }
        }
      }
    }
  } else if (d == 3 || d == 4) {
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * d;
        if (d == 4 && f[idx + 3] == 0) {
          continue; // Skip transparent pixels
        }
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        if (histB && b_val >= 0 && b_val <= 255)
          histB[b_val]++;
        if (histG && g_val >= 0 && g_val <= 255)
          histG[g_val]++;
        if (histR && r_val >= 0 && r_val <= 255)
          histR[r_val]++;
      }
    }
  }
}

__declspec(dllexport) void histogram_equalization(int *f, int w, int h, int d,
                                                  int *g) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || (d != 1 && d != 3)) {
    return;
  }

  int hist[3][256] = {0};
  int lut[3][256] = {0};
  double scale_factor = 255.0 / (double)(w * h);

  if (d == 1) {
    // 1. Calculate histogram
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = y * w + x;
        int gray_val = f[idx];
        if (gray_val >= 0 && gray_val <= 255) {
          hist[0][gray_val]++;
        }
      }
    }
    // 2. Calculate CDF and LUT
    int sum = 0;
    for (int i = 0; i < 256; i++) {
      sum += hist[0][i];
      int val = (int)(sum * scale_factor + 0.5);
      if (val < 0)
        val = 0;
      if (val > 255)
        val = 255;
      lut[0][i] = val;
    }
    // 3. Map pixels
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = y * w + x;
        int gray_val = f[idx];
        if (gray_val < 0)
          gray_val = 0;
        if (gray_val > 255)
          gray_val = 255;
        g[idx] = lut[0][gray_val];
      }
    }
  } else if (d == 3) {
    // 1. Calculate histogram
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * 3;
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        if (b_val >= 0 && b_val <= 255)
          hist[0][b_val]++;
        if (g_val >= 0 && g_val <= 255)
          hist[1][g_val]++;
        if (r_val >= 0 && r_val <= 255)
          hist[2][r_val]++;
      }
    }
    // 2. Calculate CDF and LUT
    for (int i = 0; i < 3; i++) {
      int sum = 0; // Reset sum for each channel
      for (int j = 0; j < 256; j++) {
        sum += hist[i][j];
        int val = (int)(sum * scale_factor + 0.5);
        if (val < 0)
          val = 0;
        if (val > 255)
          val = 255;
        lut[i][j] = val;
      }
    }
    // 3. Map pixels
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * 3;
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        if (b_val < 0)
          b_val = 0;
        if (b_val > 255)
          b_val = 255;
        if (g_val < 0)
          g_val = 0;
        if (g_val > 255)
          g_val = 255;
        if (r_val < 0)
          r_val = 0;
        if (r_val > 255)
          r_val = 255;

        g[idx + 0] = lut[0][b_val];
        g[idx + 1] = lut[1][g_val];
        g[idx + 2] = lut[2][r_val];
      }
    }
  }
}

}
