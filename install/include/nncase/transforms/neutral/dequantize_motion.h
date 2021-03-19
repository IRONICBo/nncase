/* Copyright 2020 Canaan Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#pragma once
#include "../transform.h"

namespace nncase::ir::transforms
{
#define DEFINE_DEQ_MOTION(name)                                                   \
    class NNCASE_API dequantize_##name##_motion_transform : public transform      \
    {                                                                             \
    public:                                                                       \
        void process(transform_context &context) override;                        \
                                                                                  \
    protected:                                                                    \
        bool skip_self_contained_check() const noexcept override { return true; } \
        bool on_try_match(ir::node &node, transform_context &context) override;   \
    };

DEFINE_DEQ_MOTION(pad)
DEFINE_DEQ_MOTION(transpose)
DEFINE_DEQ_MOTION(slice)
DEFINE_DEQ_MOTION(resize_image)
DEFINE_DEQ_MOTION(reshape)
DEFINE_DEQ_MOTION(transbin)
DEFINE_DEQ_MOTION(bitcast)
DEFINE_DEQ_MOTION(s2b)

#undef DEFINE_DEQ_MOTION
}
