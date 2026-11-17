#include "spray_emission.hpp"
#include <cassert>
#include <limits>

int main()
{
    using agribot_hil::spray_pump_fraction;
    assert(spray_pump_fraction(0., 100.) == 0.);
    assert(spray_pump_fraction(0., .0001) == 0.);
    assert(spray_pump_fraction(0., -.0001) == 0.);
    assert(spray_pump_fraction(100., 0.) == 0.);
    assert(spray_pump_fraction(100., 83.775) == .5);
    assert(spray_pump_fraction(100., 167.55) == 1.);
    assert(spray_pump_fraction(100., 1000.) == 1.);
    assert(spray_pump_fraction(100., std::numeric_limits<double>::quiet_NaN()) == 0.);
    assert(spray_pump_fraction(std::numeric_limits<double>::quiet_NaN(), 100.) == 0.);
}
