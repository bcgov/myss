import { useQuery } from "@tanstack/react-query";

import { getPlaceholderOptions } from "@/api/generated/@tanstack/react-query.gen";

export default function useDemo() {
    return useQuery({
        ...getPlaceholderOptions(),
        select: (data) => data.payload,
    });
}
