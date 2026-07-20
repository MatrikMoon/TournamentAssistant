include_guard()

# Necessary for extern.cmake
include(${CMAKE_CURRENT_SOURCE_DIR}/cmake/utils.cmake)


# read in information about the mod from qpm.json
file(READ ${CMAKE_CURRENT_SOURCE_DIR}/qpm.json PACKAGE_JSON)

string(JSON PACKAGE_INFO GET ${PACKAGE_JSON} info)

string(JSON PACKAGE_NAME GET ${PACKAGE_INFO} name)
string(JSON PACKAGE_ID GET ${PACKAGE_INFO} id)
string(JSON PACKAGE_VERSION GET ${PACKAGE_INFO} version)

message(STATUS "PACKAGE NAME: ${PACKAGE_NAME}")
message(STATUS "PACKAGE VERSION: ${PACKAGE_VERSION}")

string(JSON EXTERN_DIR_NAME GET ${PACKAGE_JSON} dependenciesDir)
string(JSON SHARED_DIR_NAME GET ${PACKAGE_JSON} sharedDir)

set(EXTERN_DIR ${CMAKE_CURRENT_SOURCE_DIR}/${EXTERN_DIR_NAME})
set(SHARED_DIR ${CMAKE_CURRENT_SOURCE_DIR}/${SHARED_DIR_NAME})

# TODO: This is empty by the time this is called
set(COMPILE_ID ${CMAKE_PROJECT_NAME})

# Setup QPM Extern
# TODO: Setup qpm extern from toolchain
cmake_language(DEFER DIRECTORY ${CMAKE_SOURCE_DIR} CALL _setup_qpm_project())
function(_setup_qpm_project)
    include(${CMAKE_CURRENT_SOURCE_DIR}/extern.cmake)
endfunction(_setup_qpm_project)