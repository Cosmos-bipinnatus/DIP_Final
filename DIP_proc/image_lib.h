#pragma once

#define DIPPROC_UNUSED(x) UNREFERENCED_PARAMETER(x)

extern "C" {
    // Color Operations
    __declspec(dllexport) void encode_gray(int *f, int w, int h, int d, int *g);
    __declspec(dllexport) void bit_plane_slice(int *f, int w, int h, int d, int *g, int plane, int binarize);

    // Intensity Operations
    __declspec(dllexport) void adjust_brightness_contrast(int *f, int w, int h, int d, int *g, double alpha, int beta);
    __declspec(dllexport) void calculate_histogram(int *f, int w, int h, int d, int *histB, int *histG, int *histR);
    __declspec(dllexport) void histogram_equalization(int *f, int w, int h, int d, int *g);

    // Geometry Operations
    __declspec(dllexport) void rotate_image(int *f, int w, int h, int d, int *g, int newW, int newH, double angle_deg, int mode, int bg_r, int bg_g, int bg_b, int bg_a);
    __declspec(dllexport) void scale_image(int *f, int w, int h, int d, int *g, int newW, int newH, int mode);

    // Threshold Operations
    __declspec(dllexport) void manual_threshold(int *f, int w, int h, int d, int *g, int T);
    __declspec(dllexport) void otsu_threshold(int *f, int w, int h, int d, int *g);

    // Filters and Edge/Line Detection
    __declspec(dllexport) void convolution_filter(int *f, int w, int h, int d, int *g, double *kernel, int kSize, double divisor, double offset);
    __declspec(dllexport) void detect_sobel(int *f, int w, int h, int d, int *g);
    __declspec(dllexport) void detect_canny(int *f, int w, int h, int d, int *g, double lowThresh, double highThresh);
    __declspec(dllexport) void detect_lines_hough(int *f, int w, int h, int d, int *g, int houghThreshold, int lineR, int lineG, int lineB);
    __declspec(dllexport) void detect_circles_hough(int *f, int w, int h, int d, int *g, int rMin, int rMax, int houghThreshold, int lineR, int lineG, int lineB);
}
