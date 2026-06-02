#include "pch.h"

#define DIPPROC_UNUSED(x) UNREFERENCED_PARAMETER(x)

// Export with C linkage using __declspec(dllexport) directly to prevent Name Mangling
extern "C" {

__declspec(dllexport) void encode_gray(int *f, int w, int h, int d, int *g) {
  if(d==3){ // is BGR img
    for(int y=0;y<h;y++){
      for(int x=0;x<w;x++){
        int idx = (y * w + x) * 3;
        double b_val=f[idx+0];
        double g_val=f[idx+1];
        double r_val=f[idx+2];
        int gray_val=(int)(r_val*0.299+g_val*0.587+b_val*0.114);
        g[idx+0]=gray_val;
        g[idx+1]=gray_val;
        g[idx+2]=gray_val;
      }
    }
  } 
  else if(d==1){ //direct output
    for(int y=0;y<h;y++){
      for(int x=0;x<w;x++){
        int idx = y * w + x;
        g[idx] = f[idx];
      }
    }
  }
}

__declspec(dllexport) void bit_plane_slice(int *f, int w, int h, int d, int *g,
                                           int plane) {
  // Establish bitmask (e.g., mask = 1 << plane)
  int mask = 1 << plane;

  for (int j = 0; j < h; j++) {
    for (int i = 0; i < w; i++) {
      int targetValue = 0;

      if (d == 1) {
        // 1. Single channel (grayscale): direct pixel value access
        int pixel = f[j * w + i];

        // Perform bitwise AND; if true, output 255, else 0
        targetValue = ((pixel & mask) != 0) ? 255 : 0;
        g[j * w + i] = targetValue;
      } else {
        // 2. Multi-channel (RGB): calculate grayscale value using weights first
        double r = f[(j * w + i) * 3 + 2];
        double g_val = f[(j * w + i) * 3 + 1];
        double b = f[(j * w + i) * 3];

        int avg = (int)(b * 0.144 + g_val * 0.587 + r * 0.299);

        // Perform bitwise AND and binarize
        targetValue = ((avg & mask) != 0) ? 255 : 0;

        // Fill R, G, B channels with the target value
        for (int k = 0; k < 3; k++) {
          g[(j * w + i) * 3 + k] = targetValue;
        }
      }
    }
  }
}

__declspec(dllexport) void adjust_brightness_contrast(int *f, int w, int h, int d, int *g,
                                                      double alpha, int beta) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(alpha);
  DIPPROC_UNUSED(beta);
}

__declspec(dllexport) void calculate_histogram(int *f, int w, int h, int d,
                                               int *histB, int *histG, int *histR) {
  // Initialize histograms to 0
  if (histB) {
    for (int i = 0; i < 256; i++) histB[i] = 0;
  }
  if (histG) {
    for (int i = 0; i < 256; i++) histG[i] = 0;
  }
  if (histR) {
    for (int i = 0; i < 256; i++) histR[i] = 0;
  }

  if (d == 1) {
    if (histB) {
      for (int y = 0; y < h; y++) {
        for (int x = 0; x < w; x++) {
          int idx = y * w + x;
          int val = f[idx];
          if (val >= 0 && val <= 255) {
            histB[val]++;
          }
        }
      }
    }
  }
  else if (d == 3) {
    for (int y = 0; y < h; y++) {
      for (int x = 0; x < w; x++) {
        int idx = (y * w + x) * 3;
        int b_val = f[idx + 0];
        int g_val = f[idx + 1];
        int r_val = f[idx + 2];

        if (histB && b_val >= 0 && b_val <= 255) histB[b_val]++;
        if (histG && g_val >= 0 && g_val <= 255) histG[g_val]++;
        if (histR && r_val >= 0 && r_val <= 255) histR[r_val]++;
      }
    }
  }
}


__declspec(dllexport) void histogram_equalization(int *f, int w, int h, int d, int *g) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
}

__declspec(dllexport) void spatial_filter(int *f, int w, int h, int d, int *g,
                                          double *kernel, int kSize, double divisor,
                                          double offset) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(kernel);
  DIPPROC_UNUSED(kSize);
  DIPPROC_UNUSED(divisor);
  DIPPROC_UNUSED(offset);
}

__declspec(dllexport) void scale_image(int *f, int w, int h, int d, int *g, int newW,
                                       int newH, int mode) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(newW);
  DIPPROC_UNUSED(newH);
  DIPPROC_UNUSED(mode);
}

__declspec(dllexport) void rotate_image(int *f, int w, int h, int d, int *g, int newW,
                                        int newH, double angle_deg, int mode) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(newW);
  DIPPROC_UNUSED(newH);
  DIPPROC_UNUSED(angle_deg);
  DIPPROC_UNUSED(mode);
}

__declspec(dllexport) void manual_threshold(int *f, int w, int h, int d, int *g, int T) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(T);
}

__declspec(dllexport) void otsu_threshold(int *f, int w, int h, int d, int *g) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
}

__declspec(dllexport) void detect_sobel(int *f, int w, int h, int d, int *g) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
}

__declspec(dllexport) void detect_canny(int *f, int w, int h, int d, int *g,
                                        double lowThresh, double highThresh) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(lowThresh);
  DIPPROC_UNUSED(highThresh);
}

__declspec(dllexport) void detect_lines_hough(int *f, int w, int h, int d, int *g,
                                              int houghThreshold) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(houghThreshold);
}

__declspec(dllexport) void detect_circles_hough(int *f, int w, int h, int d, int *g,
                                                int rMin, int rMax, int houghThreshold) {
  DIPPROC_UNUSED(f);
  DIPPROC_UNUSED(w);
  DIPPROC_UNUSED(h);
  DIPPROC_UNUSED(d);
  DIPPROC_UNUSED(g);
  DIPPROC_UNUSED(rMin);
  DIPPROC_UNUSED(rMax);
  DIPPROC_UNUSED(houghThreshold);
}

}
