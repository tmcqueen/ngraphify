cmake_minimum_required(VERSION 3.16)
project(leiden_interop CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Find installed libleidenalg and igraph via CMake config packages
find_package(libleidenalg REQUIRED
    HINTS /usr/lib/x86_64-linux-gnu/cmake/libleidenalg
)
find_package(igraph REQUIRED)

# Create the shared interop library
add_library(leiden_interop SHARED leiden_interop.cpp)

target_include_directories(leiden_interop PRIVATE
    /usr/include
)

target_link_libraries(leiden_interop PRIVATE
    libleidenalg::libleidenalg
    igraph::igraph
)

# Output to lib/ subdirectory
set_target_properties(leiden_interop PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
    PREFIX "lib"
)
