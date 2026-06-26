/*
 * Petalas-Aziz Multiphase Flow Correlation (ARM64 native)
 * Based on SPE 56525 - A Mechanistic Model for Multiphase Flow in Pipes
 * Replaces the original Fortran libPetAz for ARM64 macOS
 */

#include <math.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Helper: Reynolds number */
static double reynolds(double rho, double v, double d, double mu) {
    /* rho: lb/ft3, v: ft/s, d: ft, mu: cP */
    return 1488.0 * rho * v * d / mu;
}

/* Helper: Colebrook-White friction factor */
static double colebrook(double re, double rough, double d) {
    double f;
    if (re < 2000.0) {
        f = 64.0 / re;
    } else {
        double relRough = rough / d;
        double fguess = 1.0 / pow(-2.0 * log10(relRough / 3.7 + 5.74 / (pow(re, 0.9))), 2.0);
        for (int i = 0; i < 20; i++) {
            double rhs = -2.0 * log10(relRough / 3.7 + 2.51 / (re * sqrt(fguess)));
            fguess = 1.0 / (rhs * rhs);
        }
        f = fguess;
    }
    return f;
}

void calcpdrop_(float *DensL, float *DensG, float *MuL, float *MuG,
                float *Sigma, float *Dia, float *Rough, float *Theta,
                float *VsL, float *VsG, int *Region, float *dPfr, float *dPhh, float *eL) {
    
    double rhol = (double)*DensL;
    double rhog = (double)*DensG;
    double mul = (double)*MuL;
    double mug = (double)*MuG;
    double sigma = (double)*Sigma;
    double dia_in = (double)*Dia;
    double rough = (double)*Rough;
    double theta = (double)*Theta;
    double vsl = (double)*VsL;
    double vsg = (double)*VsG;
    
    /* Convert diameter from inches to feet */
    double d_ft = dia_in / 12.0;
    
    /* Mixture velocity */
    double vm = vsl + vsg;
    
    /* No-slip liquid fraction */
    double lambda_l = (vm > 1e-10) ? vsl / vm : 1.0;
    
    /* No-slip mixture density */
    double rho_m = lambda_l * rhol + (1.0 - lambda_l) * rhog;
    
    /* No-slip mixture viscosity */
    double mu_m = lambda_l * mul + (1.0 - lambda_l) * mug;
    
    /* Gravity component */
    double g = 32.174;  /* ft/s2 */
    double theta_rad = theta * 3.14159265358979 / 180.0;
    double sintheta = sin(theta_rad);
    
    /* Dimensionless parameters for flow regime */
    double Nlv = vsl * pow(rhol / (g * sigma), 0.25);  /* liquid velocity number */
    double Ngv = vsg * pow(rhol / (g * sigma), 0.25);  /* gas velocity number */
    
    /* Flow regime determination (simplified Petalas-Aziz) */
    double fric;
    double holdup;
    int regime;
    
    /* Bubble flow check */
    double lambda_bubble = 0.13;
    double dbubble = 0.0;
    
    /* Check for dispersed bubble flow */
    if (vsl > 0.5 && vm > 6.0 * pow(sigma / (rhol - rhog), 0.25) * pow(g, 0.25)) {
        /* Dispersed bubble flow */
        regime = 1;
        holdup = lambda_l;  /* approximate */
        double re_m = reynolds(rho_m, vm, d_ft, mu_m);
        fric = colebrook(re_m, rough, d_ft);
    }
    /* Bubble flow */
    else if (vsl > 0.5 && lambda_l > 0.6 && vsg < 2.0) {
        regime = 2;
        /* Bubble flow holdup */
        double vsb = 1.53 * pow(g * sigma * (rhol - rhog) / (rhol * rhol), 0.25);
        double c0 = 1.2;
        if (vm > 1e-10) {
            holdup = vsl / (vm * c0 + vsb * (1.0 - lambda_l));
            if (holdup > 1.0) holdup = 1.0;
            if (holdup < lambda_l) holdup = lambda_l;
        } else {
            holdup = 1.0;
        }
        double re_m = reynolds(rho_m, vm, d_ft, mu_m);
        fric = colebrook(re_m, rough, d_ft);
    }
    /* Slug flow */
    else if (lambda_l > 0.01 && vm * vm < 25.0 * pow(g * d_ft, 0.5)) {
        regime = 3;
        /* Slug flow holdup (simplified) */
        double vtb = 1.53 * pow(g * sigma * (rhol - rhog) / (rhol * rhol), 0.25);
        double c0 = 1.2;
        if (vm > 1e-10) {
            holdup = vsl / (vm * c0 + vtb * (1.0 - lambda_l));
            if (holdup > 1.0) holdup = 1.0;
            if (holdup < lambda_l) holdup = lambda_l;
        } else {
            holdup = 1.0;
        }
        /* Friction factor using mixture */
        double rho_tp = holdup * rhol + (1.0 - holdup) * rhog;
        double mu_tp = holdup * mul + (1.0 - holdup) * mug;
        double re_tp = reynolds(rho_tp, vm, d_ft, mu_tp);
        fric = colebrook(re_tp, rough, d_ft) * pow(holdup / lambda_l > 0 ? holdup / lambda_l : 1.0, 0.5);
    }
    /* Annular flow */
    else if (vsg > 30.0 && lambda_l < 0.1) {
        regime = 4;
        /* Annular flow: simplified holdup */
        holdup = lambda_l * 0.8;
        if (holdup < 0.001) holdup = 0.001;
        /* Friction factor based on gas */
        double re_g = reynolds(rhog, vsg, d_ft, mug);
        fric = colebrook(re_g, rough, d_ft);
        /* Enhancement for liquid film */
        fric *= (1.0 + 10.0 * lambda_l);
    }
    /* Stratified flow (horizontal/near-horizontal) */
    else if (fabs(sintheta) < 0.1 && vsg < 30.0) {
        regime = 5;
        holdup = lambda_l;
        double re_m = reynolds(rho_m, vm, d_ft, mu_m);
        fric = colebrook(re_m, rough, d_ft);
    }
    /* Default: use mixture model */
    else {
        regime = 6;
        holdup = lambda_l;
        double re_m = reynolds(rho_m, vm, d_ft, mu_m);
        fric = colebrook(re_m, rough, d_ft);
    }
    
    /* Ensure holdup is in valid range */
    if (holdup < 0.0) holdup = 0.0;
    if (holdup > 1.0) holdup = 1.0;
    
    /* Mixture density with holdup */
    double rho_tp = holdup * rhol + (1.0 - holdup) * rhog;
    
    /* Frictional pressure gradient (psi/ft) */
    /* dP/dx = f * rho * v^2 / (2 * g_c * D)  [lbf/ft2/ft] */
    /* Convert to psi/ft: divide by 144 */
    double gc = 32.174;
    double dPfriction = fric * rho_tp * vm * vm / (2.0 * gc * d_ft) / 144.0;
    
    /* Hydrostatic pressure gradient (psi/ft) */
    /* dP/dx = rho * g * sin(theta) / gc / 144 */
    double dPhydro = rho_tp * sintheta / 144.0;
    
    /* Output */
    *Region = regime;
    *dPfr = (float)dPfriction;
    *dPhh = (float)dPhydro;
    *eL = (float)holdup;
}

/* Also provide the lowercase name for compatibility */
void calcpdrop(float *DensL, float *DensG, float *MuL, float *MuG,
               float *Sigma, float *Dia, float *Rough, float *Theta,
               float *VsL, float *VsG, int *Region, float *dPfr, float *dPhh, float *eL) {
    calcpdrop_(DensL, DensG, MuL, MuG, Sigma, Dia, Rough, Theta, VsL, VsG, Region, dPfr, dPhh, eL);
}

#ifdef __cplusplus
}
#endif
