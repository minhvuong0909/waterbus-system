// màn hình
import { View, Text } from "react-native";
import { useProducts } from "../hooks/useProducts";

export default function HomeScreen() {
  const { data, isLoading } = useProducts();

  if (isLoading) return <Text>Loading...</Text>;

  return (
    <View>
      {data?.map((p: { id: number; name: string }) => (
        <Text key={p.id}>{p.name}</Text>
      ))}
    </View>
  );
}
