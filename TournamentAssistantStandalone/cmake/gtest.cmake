include_guard()

message("Compiling with GTest")

# GTest
include(FetchContent)
FetchContent_Declare(
    googletest
    URL https://github.com/google/googletest/archive/03597a01ee50ed33e9dfd640b249b4be3799d395.zip
)

# For Windows: Prevent overriding the parent project's compiler/linker settings
set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(googletest)

enable_testing()

# Run at end to link with project
cmake_language(DEFER DIRECTORY ${CMAKE_SOURCE_DIR} CALL _setup_gtest_project())

function(_setup_gtest_project)
    # recursively get all src files
    RECURSE_FILES(cpp_test_file_list ${CMAKE_CURRENT_SOURCE_DIR}/test/*.cpp)
    RECURSE_FILES(c_test_file_list ${CMAKE_CURRENT_SOURCE_DIR}/test/*.c)

    add_executable(
        ${PROJECT_NAME}_test
        ${cpp_test_file_list}
        ${c_test_file_list}
    )
    target_link_libraries(
        ${PROJECT_NAME}_test
        PRIVATE ${PROJECT_NAME}
        GTest::gtest_main
    )

    target_include_directories(${PROJECT_NAME}_test PRIVATE ${INCLUDE_DIR})
    target_include_directories(${PROJECT_NAME}_test PRIVATE ${SHARED_DIR})
    target_include_directories(${PROJECT_NAME}_test PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/test)

    include(GoogleTest)
    gtest_discover_tests(${PROJECT_NAME}_test)
endfunction(_setup_gtest_project)
