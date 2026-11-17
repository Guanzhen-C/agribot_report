// Copyright 2026 Agribot contributors. Licensed under the Apache License, Version 2.0.
#ifndef GAZEBO_PLUGINS__WHEEL_VELOCITY_LIMIT_HPP_
#define GAZEBO_PLUGINS__WHEEL_VELOCITY_LIMIT_HPP_

#include <algorithm>

namespace gazebo_plugins
{
inline double limit_wheel_velocity(
  double previous_command, double target, double acceleration, double elapsed)
{
  if (acceleration == 0.0) {
    return target;
  }
  const double step = acceleration * std::max(0.0, elapsed);
  return previous_command + std::max(-step, std::min(step, target - previous_command));
}
}  // namespace gazebo_plugins

#endif  // GAZEBO_PLUGINS__WHEEL_VELOCITY_LIMIT_HPP_
