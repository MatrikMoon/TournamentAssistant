include_guard()

include(${CMAKE_CURRENT_SOURCE_DIR}/cmake/targets/android-ndk.cmake)
include(${CMAKE_CURRENT_SOURCE_DIR}/cmake/qpm.cmake)

add_compile_definitions(QUEST)
add_compile_definitions(UNITY_2021)
add_compile_definitions(NEED_UNSAFE_CSHARP)