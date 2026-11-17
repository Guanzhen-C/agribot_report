#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>
#include "nav2_core/controller.hpp"
#include "nav2_core/exceptions.hpp"
#include "pluginlib/class_loader.hpp"

int main(int argc, char ** argv)
{
  const auto domain = std::getenv("ROS_DOMAIN_ID");
  const auto local = std::getenv("ROS_LOCALHOST_ONLY");
  if (!domain || std::string(domain) != "225" || !local || std::string(local) != "1") {
    std::cerr << "Software-only probe requires isolated localhost domain 225\n";
    return 2;
  }
  rclcpp::init(argc, argv);
  int result = 1;
  try {
    rclcpp::NodeOptions options;
    options.parameter_overrides({
      rclcpp::Parameter("controller_frequency", 10.0),
      rclcpp::Parameter("FollowPath.model_dt", .1),
      rclcpp::Parameter("FollowPath.time_steps", 10),
      rclcpp::Parameter("FollowPath.batch_size", 16),
      rclcpp::Parameter("FollowPath.critics", std::vector<std::string>{"CostCritic"}),
      rclcpp::Parameter("FollowPath.CostCritic.consider_footprint", true),
      rclcpp::Parameter("FollowPath.visualize", false)});
    options.automatically_declare_parameters_from_overrides(true);
    auto node = std::make_shared<rclcpp_lifecycle::LifecycleNode>("hil_mppi_contract_probe", "", options);
    auto map = std::make_shared<nav2_costmap_2d::Costmap2DROS>("hil_contract_costmap");
    map->set_parameter(rclcpp::Parameter("plugins", std::vector<std::string>{}));
    map->set_parameter(rclcpp::Parameter("global_frame", "map"));
    map->set_parameter(rclcpp::Parameter("footprint", "[[0.4,0.3],[0.4,-0.3],[-0.4,-0.3],[-0.4,0.3]]"));
    map->configure();
    auto grid = map->getCostmap();
    grid->resizeMap(80, 80, .05, -2., -2.);
    std::fill_n(grid->getCharMap(), 80*80, nav2_costmap_2d::LETHAL_OBSTACLE);
    pluginlib::ClassLoader<nav2_core::Controller> loader("nav2_core", "nav2_core::Controller");
    auto controller = loader.createSharedInstance("nav2_mppi_controller::MPPIController");
    controller->configure(node, "FollowPath", map->getTfBuffer(), map);
    pluginlib::ClassLoader<nav2_core::GoalChecker> goal_loader("nav2_core", "nav2_core::GoalChecker");
    auto checker = goal_loader.createSharedInstance("nav2_controller::SimpleGoalChecker");
    checker->initialize(node, "goal_checker", map);
    controller->activate();
    nav_msgs::msg::Path path;
    path.header.frame_id = "map";
    path.header.stamp = node->now();
    geometry_msgs::msg::PoseStamped pose;
    pose.header = path.header;
    pose.pose.orientation.w = 1.;
    for (int i = 0; i <= 20; ++i) {
      auto point = pose;
      point.pose.position.x = i*.05;
      path.poses.push_back(point);
    }
    controller->setPlan(path);
    unsigned typed_failures = 0;
    for (int attempt = 0; attempt < 2; ++attempt) {
      try {
        controller->computeVelocityCommands(pose, geometry_msgs::msg::Twist(), checker.get());
      } catch (const nav2_core::PlannerException & error) {
        if (std::string(error.what()) != "Optimizer fail to compute path") {throw;}
        ++typed_failures;
      }
    }
    if (typed_failures != 2) {throw std::runtime_error("Blocked fixture did not fail closed twice");}
    std::fill_n(grid->getCharMap(), 80*80, nav2_costmap_2d::FREE_SPACE);
    const auto command = controller->computeVelocityCommands(pose, geometry_msgs::msg::Twist(), checker.get());
    if (!std::isfinite(command.twist.linear.x) || !std::isfinite(command.twist.angular.z)) {
      throw std::runtime_error("Recovery produced a nonfinite command");
    }
    controller->deactivate();
    controller->cleanup();
    checker.reset();
    controller.reset();
    map->cleanup();
    map.reset();
    node.reset();
    std::cout << "{\"passed\":true,\"actual_native_optimizer_typed_failures\":2,"
                 "\"same_controller_recovered\":true,\"hardware_outputs\":false}" << std::endl;
    result = 0;
  } catch (const std::exception & error) {
    std::cerr << "Contract probe failed: " << error.what() << '\n';
  }
  rclcpp::shutdown();
  return result;
}
