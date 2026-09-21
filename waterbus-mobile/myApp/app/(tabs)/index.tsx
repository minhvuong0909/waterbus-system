import { View, Text, Button } from "react-native";
import { router } from "expo-router";

export default function Home() {
  return (
    <View style={{ flex: 1, justifyContent: "center", alignItems: "center" }}>
      <Text>Home Screen</Text>

      <Button title="Open modal" onPress={() => router.push("/modal")} />
    </View>
  );
}
