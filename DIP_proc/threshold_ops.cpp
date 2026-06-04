#include "pch.h"
#include "image_lib.h"

extern "C" {

__declspec(dllexport) void manual_threshold(int *f, int w, int h, int d, int *g,
                                            int T) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0) {
    return;
  }
  int total = w * h;
  for (int i = 0; i < total; i++) {
    if (d == 1) {
      g[i] = (f[i] >= T) ? 255 : 0;
    } else {
      int idx = i * d;
      double b_val = f[idx + 0];
      double g_val = f[idx + 1];
      double r_val = f[idx + 2];
      int gray_val = (int)(r_val * 0.299 + g_val * 0.587 + b_val * 0.114 + 0.5);
      int bin_val = (gray_val >= T) ? 255 : 0;
      g[idx + 0] = bin_val;
      g[idx + 1] = bin_val;
      g[idx + 2] = bin_val;
      if (d == 4) {
        g[idx + 3] = f[idx + 3]; // Preserve original Alpha
      }
    }
  }
}

__declspec(dllexport) void otsu_threshold(int *f, int w, int h, int d, int *g) {
  if (f == nullptr || g == nullptr || w <= 0 || h <= 0 || d <= 0) {
    return;
  }

  long long total_pixels = (long long)w * h;
  int hist[256] = {0};
  double total_sum = 0.0;

  // 1. Calculate Grayscale Histogram
  for (int i = 0; i < total_pixels; i++) {
    int gray_val = 0;
    if (d == 1) {
      gray_val = f[i];
    } else {
      int idx = i * d;
      double b = f[idx + 0];
      double g_val = f[idx + 1];
      double r = f[idx + 2];
      gray_val = (int)(r * 0.299 + g_val * 0.587 + b * 0.114 + 0.5);
    }
    if (gray_val < 0) gray_val = 0;
    if (gray_val > 255) gray_val = 255;
    hist[gray_val]++;
    total_sum += gray_val;
  }

  // 2. Otsu threshold calculation
  double sum_bg = 0.0;
  long long w_bg = 0;
  double max_variance = -1.0;
  int best_threshold = 0;

  for (int t = 0; t < 256; t++) {
    w_bg += hist[t];
    if (w_bg == 0) continue;
    long long w_fg = total_pixels - w_bg;
    if (w_fg == 0) break;

    sum_bg += (double)t * hist[t];

    double mean_bg = sum_bg / w_bg;
    double mean_fg = (total_sum - sum_bg) / w_fg;

    // Between-class variance
    double variance = (double)w_bg * (double)w_fg * (mean_bg - mean_fg) * (mean_bg - mean_fg);

    if (variance > max_variance) {
      max_variance = variance;
      best_threshold = t;
    }
  }

  // 3. Binarization mapping
  for (int i = 0; i < total_pixels; i++) {
    if (d == 1) {
      g[i] = (f[i] >= best_threshold) ? 255 : 0;
    } else {
      int idx = i * d;
      double b = f[idx + 0];
      double g_val = f[idx + 1];
      double r = f[idx + 2];
      int gray_val = (int)(r * 0.299 + g_val * 0.587 + b * 0.114 + 0.5);
      int bin_val = (gray_val >= best_threshold) ? 255 : 0;
      g[idx + 0] = bin_val;
      g[idx + 1] = bin_val;
      g[idx + 2] = bin_val;
      if (d == 4) {
        g[idx + 3] = f[idx + 3]; // Preserve original Alpha
      }
    }
  }
}

}
