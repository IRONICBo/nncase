﻿cmake_minimum_required (VERSION 3.8)

set(SRCS module_builder.cpp
         op_writer.cpp
         ops/batch_to_space.cpp
         ops/binary.cpp
         ops/broadcast.cpp
         ops/call.cpp
         ops/conv2d.cpp
         ops/convert.cpp
         ops/copy.cpp
         ops/cumsum.cpp
         ops/dequantize.cpp
         ops/gather.cpp
         ops/gather_nd.cpp
         ops/hardmax.cpp
         ops/onehot.cpp
         ops/pad.cpp
         ops/quantize.cpp
         ops/random_normal.cpp
         ops/random_uniform.cpp
         ops/reduce.cpp
         ops/reduce_arg.cpp
         ops/reduce_prod.cpp
         ops/reduce_window2d.cpp
         ops/resize_image.cpp
         ops/slice.cpp
         ops/table_lookup1d.cpp
         ops/ternary.cpp
         ops/transpose.cpp
         ops/unary.cpp)

add_library(codegen_stackvm OBJECT ${SRCS})
target_link_libraries(codegen_stackvm PUBLIC ir schedule)
target_link_libraries(codegen_stackvm PRIVATE evaluator mpark_variant::mpark_variant)
target_compile_definitions(codegen_stackvm PUBLIC -DNNCASE_DLL)
set_target_properties(codegen_stackvm PROPERTIES POSITION_INDEPENDENT_CODE ON)