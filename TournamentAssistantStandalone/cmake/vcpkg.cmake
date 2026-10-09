include_guard()

# vcpkg config
message("VCPKG $ENV{VCPKG_ROOT}")

# chain the toolchain file for vcpkg
if(CMAKE_TOOLCHAIN_FILE NOT MATCHES ".+")
    set(VCPKG_CHAINLOAD_TOOLCHAIN_FILE ${CMAKE_TOOLCHAIN_FILE})
endif()

string(REPLACE "\\" "/" VCPKG_ROOT_WINDOWS_FIX $ENV{VCPKG_ROOT})
set(CMAKE_TOOLCHAIN_FILE ${VCPKG_ROOT_WINDOWS_FIX}/scripts/buildsystems/vcpkg.cmake)