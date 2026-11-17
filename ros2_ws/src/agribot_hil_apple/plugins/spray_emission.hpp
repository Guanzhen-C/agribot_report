#pragma once
#include <algorithm>
#include <cmath>

namespace agribot_hil {
inline double spray_pump_fraction(double requested_rad_s, double actual_rad_s)
{
    if (!std::isfinite(requested_rad_s) || requested_rad_s <= 0. || !std::isfinite(actual_rad_s)) {
        return 0.;
    }
    return std::clamp(std::abs(actual_rad_s) / 167.55, 0., 1.);
}
}
