import { useMutation } from "@tanstack/react-query";

import { submitBusPass } from "@/api/busPass";

// The bus pass submit mutation. The spec itself comes through useFormSpec in
// usePocForm, since the bus pass form is served by the same forms endpoint.

export function useSubmitBusPass() {
    return useMutation({ mutationFn: submitBusPass });
}
